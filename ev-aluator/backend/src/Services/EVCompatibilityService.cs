using EVRangeAnalyzer.Models;

namespace EVRangeAnalyzer.Services;

/// <summary>
/// Service for analyzing EV range compatibility with historical driving patterns.
/// Calculates daily trip summaries, range requirements, and provides comprehensive
/// compatibility analysis with recommendations for EV adoption.
/// </summary>
public interface IEVCompatibilityService
{
    /// <summary>
    /// Analyzes EV compatibility for a given range against historical location data.
    /// </summary>
    /// <param name="sessionId">Session ID for the analysis.</param>
    /// <param name="locationPoints">Historical location points to analyze.</param>
    /// <param name="evRangeKm">EV single-charge range in kilometers.</param>
    /// <returns>Complete EV range compatibility analysis.</returns>
    Task<EVRangeAnalysis> AnalyzeCompatibilityAsync(
        string sessionId, 
        IEnumerable<LocationPoint> locationPoints, 
        int evRangeKm);

    /// <summary>
    /// Calculates daily trip summaries from location points.
    /// </summary>
    /// <param name="sessionId">Session ID for the summaries.</param>
    /// <param name="locationPoints">Location points to summarize by day.</param>
    /// <returns>Daily trip summaries grouped by date.</returns>
    Task<List<DailyTripSummary>> CalculateDailySummariesAsync(
        string sessionId, 
        IEnumerable<LocationPoint> locationPoints);

    /// <summary>
    /// Calculates the minimum EV range required for a specific compatibility percentage.
    /// </summary>
    /// <param name="dailySummaries">Daily trip summaries to analyze.</param>
    /// <param name="targetCompatibilityPercentage">Desired compatibility percentage (0-100).</param>
    /// <returns>Minimum EV range required in kilometers.</returns>
    int CalculateRequiredRange(IEnumerable<DailyTripSummary> dailySummaries, double targetCompatibilityPercentage = 95.0);

    /// <summary>
    /// Generates EV range recommendations based on historical driving patterns.
    /// </summary>
    /// <param name="dailySummaries">Daily trip summaries to analyze.</param>
    /// <returns>Range recommendations with compatibility percentages.</returns>
    Task<List<RangeRecommendation>> GenerateRangeRecommendationsAsync(IEnumerable<DailyTripSummary> dailySummaries);

    /// <summary>
    /// Analyzes seasonal driving patterns and their impact on EV compatibility.
    /// </summary>
    /// <param name="dailySummaries">Daily trip summaries to analyze.</param>
    /// <returns>Seasonal analysis with monthly compatibility data.</returns>
    Task<SeasonalAnalysis> AnalyzeSeasonalPatternsAsync(IEnumerable<DailyTripSummary> dailySummaries);

    /// <summary>
    /// Identifies challenging driving days that would require special planning with an EV.
    /// </summary>
    /// <param name="dailySummaries">Daily trip summaries to analyze.</param>
    /// <param name="evRangeKm">EV range to analyze against.</param>
    /// <returns>Days that exceed EV range with details.</returns>
    Task<List<ChallengingDay>> IdentifyChallengingDaysAsync(
        IEnumerable<DailyTripSummary> dailySummaries, 
        int evRangeKm);

    /// <summary>
    /// Validates that daily summaries are suitable for EV compatibility analysis.
    /// </summary>
    /// <param name="dailySummaries">Daily summaries to validate.</param>
    /// <returns>Validation result with any issues found.</returns>
    CompatibilityAnalysisValidation ValidateAnalysisData(IEnumerable<DailyTripSummary> dailySummaries);
}

/// <summary>
/// Implementation of EV compatibility service with comprehensive analysis capabilities.
/// </summary>
public class EVCompatibilityService : IEVCompatibilityService
{
    private readonly ILocationAnalysisService _locationAnalysisService;
    private readonly IDistanceCalculationService _distanceCalculationService;
    private readonly ILogger<EVCompatibilityService> _logger;

    /// <summary>
    /// Minimum number of driving days required for meaningful analysis.
    /// </summary>
    private const int MinAnalysisDays = 7;

    /// <summary>
    /// Standard EV ranges for recommendations (in kilometers).
    /// </summary>
    private static readonly int[] StandardEVRanges = { 150, 200, 250, 300, 350, 400, 450, 500, 600, 700 };

    public EVCompatibilityService(
        ILocationAnalysisService locationAnalysisService,
        IDistanceCalculationService distanceCalculationService,
        ILogger<EVCompatibilityService> logger)
    {
        _locationAnalysisService = locationAnalysisService ?? throw new ArgumentNullException(nameof(locationAnalysisService));
        _distanceCalculationService = distanceCalculationService ?? throw new ArgumentNullException(nameof(distanceCalculationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<EVRangeAnalysis> AnalyzeCompatibilityAsync(
        string sessionId,
        IEnumerable<LocationPoint> locationPoints,
        int evRangeKm)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        if (evRangeKm <= 0 || evRangeKm > 1000)
            throw new ArgumentException("EV range must be between 1 and 1000 kilometers", nameof(evRangeKm));

        var points = locationPoints?.ToList() ?? throw new ArgumentNullException(nameof(locationPoints));

        _logger.LogInformation("Starting EV compatibility analysis for {PointCount} location points with {EVRange}km range",
            points.Count, evRangeKm);

        try
        {
            // Calculate daily trip summaries
            var dailySummaries = await CalculateDailySummariesAsync(sessionId, points);
            
            // Validate analysis data
            var validation = ValidateAnalysisData(dailySummaries);
            if (!validation.IsValid)
            {
                var errorMessage = string.Join("; ", validation.Errors);
                _logger.LogWarning("EV compatibility analysis validation failed: {Errors}", errorMessage);
                
                return new EVRangeAnalysis
                {
                    SessionId = sessionId,
                    EVRangeKm = evRangeKm,
                    AnalysisMetadata = new Dictionary<string, object>
                    {
                        ["ValidationErrors"] = validation.Errors,
                        ["Warning"] = "Analysis may be unreliable due to insufficient data"
                    }
                };
            }

            // Create comprehensive analysis
            var analysis = EVRangeAnalysis.CreateFromDailySummaries(sessionId, evRangeKm, dailySummaries);

            _logger.LogInformation("Completed EV compatibility analysis: {Compatibility:F1}% compatibility " +
                                 "for {EVRange}km range across {Days} days",
                analysis.CompatibilityPercentage, evRangeKm, analysis.TotalDaysAnalyzed);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze EV compatibility for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<DailyTripSummary>> CalculateDailySummariesAsync(
        string sessionId,
        IEnumerable<LocationPoint> locationPoints)
    {
        var points = locationPoints?.ToList() ?? throw new ArgumentNullException(nameof(locationPoints));
        
        if (!points.Any())
        {
            _logger.LogDebug("No location points provided for daily summary calculation");
            return new List<DailyTripSummary>();
        }

        _logger.LogDebug("Calculating daily summaries for {PointCount} location points", points.Count);

        // Group points by date (local timezone)
        var pointsByDate = points
            .Where(p => p.ShouldIncludeInAnalysis())
            .GroupBy(p => DateOnly.FromDateTime(p.Timestamp.Date))
            .OrderBy(g => g.Key)
            .ToList();

        var dailySummaries = new List<DailyTripSummary>();

        foreach (var dateGroup in pointsByDate)
        {
            try
            {
                var datePoints = dateGroup.OrderBy(p => p.Timestamp).ToList();
                var summary = DailyTripSummary.CalculateFromLocationPoints(sessionId, dateGroup.Key, datePoints);
                
                // Only include days with meaningful driving activity
                if (summary.IsSignificantDrivingDay())
                {
                    dailySummaries.Add(summary);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate daily summary for {Date}", dateGroup.Key);
                // Continue with other dates
            }
        }

        _logger.LogInformation("Calculated {SummaryCount} daily summaries from {DateGroups} date groups",
            dailySummaries.Count, pointsByDate.Count);

        return dailySummaries;
    }

    /// <inheritdoc />
    public int CalculateRequiredRange(IEnumerable<DailyTripSummary> dailySummaries, double targetCompatibilityPercentage = 95.0)
    {
        var summaries = dailySummaries?.ToList() ?? throw new ArgumentNullException(nameof(dailySummaries));
        
        if (!summaries.Any())
        {
            return 0;
        }

        if (targetCompatibilityPercentage < 0 || targetCompatibilityPercentage > 100)
        {
            throw new ArgumentException("Target compatibility percentage must be between 0 and 100", 
                nameof(targetCompatibilityPercentage));
        }

        // Get all longest daily trips and sort them
        var longestTrips = summaries
            .Select(s => s.LongestTripKm)
            .OrderBy(distance => distance)
            .ToList();

        if (!longestTrips.Any())
        {
            return 0;
        }

        // Calculate the percentile index
        var targetIndex = (int)Math.Ceiling(longestTrips.Count * (targetCompatibilityPercentage / 100.0)) - 1;
        targetIndex = Math.Max(0, Math.Min(targetIndex, longestTrips.Count - 1));

        var requiredRange = (int)Math.Ceiling(longestTrips[targetIndex]);

        _logger.LogDebug("Calculated required range: {Range}km for {Percentage:F1}% compatibility " +
                        "based on {TripCount} trips",
            requiredRange, targetCompatibilityPercentage, longestTrips.Count);

        return requiredRange;
    }

    /// <inheritdoc />
    public async Task<List<RangeRecommendation>> GenerateRangeRecommendationsAsync(IEnumerable<DailyTripSummary> dailySummaries)
    {
        var summaries = dailySummaries?.ToList() ?? throw new ArgumentNullException(nameof(dailySummaries));
        var recommendations = new List<RangeRecommendation>();

        if (!summaries.Any())
        {
            return recommendations;
        }

        foreach (var range in StandardEVRanges)
        {
            try
            {
                var compatibleDays = summaries.Count(s => s.IsCompatibleWithEVRange(range));
                var compatibilityPercentage = (double)compatibleDays / summaries.Count * 100;

                var recommendation = new RangeRecommendation
                {
                    EVRangeKm = range,
                    CompatibilityPercentage = compatibilityPercentage,
                    CompatibleDays = compatibleDays,
                    TotalDays = summaries.Count,
                    Assessment = GetCompatibilityAssessment(compatibilityPercentage),
                    EstimatedPrice = EstimateEVPrice(range),
                    IsRecommended = compatibilityPercentage >= 85 && compatibilityPercentage < 100,
                    Notes = GenerateRangeNotes(compatibilityPercentage, range, summaries)
                };

                recommendations.Add(recommendation);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate recommendation for {Range}km range", range);
            }
        }

        // Sort by compatibility percentage (descending) and then by range (ascending)
        recommendations = recommendations
            .OrderByDescending(r => r.CompatibilityPercentage)
            .ThenBy(r => r.EVRangeKm)
            .ToList();

        _logger.LogDebug("Generated {RecommendationCount} range recommendations", recommendations.Count);

        return recommendations;
    }

    /// <inheritdoc />
    public async Task<SeasonalAnalysis> AnalyzeSeasonalPatternsAsync(IEnumerable<DailyTripSummary> dailySummaries)
    {
        var summaries = dailySummaries?.ToList() ?? throw new ArgumentNullException(nameof(dailySummaries));
        
        var analysis = new SeasonalAnalysis();

        if (!summaries.Any())
        {
            return analysis;
        }

        // Group by month
        var monthlyGroups = summaries
            .GroupBy(s => s.Date.Month)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var monthGroup in monthlyGroups)
        {
            var monthSummaries = monthGroup.ToList();
            var monthlyData = new MonthlyData
            {
                Month = monthGroup.Key,
                TotalDays = monthSummaries.Count,
                AverageDistance = monthSummaries.Average(s => s.TotalDistanceKm),
                MaxDistance = monthSummaries.Max(s => s.TotalDistanceKm),
                AverageTrips = (int)monthSummaries.Average(s => s.MotorizedTrips),
                AverageSpeed = monthSummaries.Average(s => s.AverageSpeedKmh)
            };

            // Calculate compatibility for different ranges
            foreach (var range in new[] { 200, 300, 400, 500 })
            {
                var compatibleDays = monthSummaries.Count(s => s.IsCompatibleWithEVRange(range));
                var compatibility = (double)compatibleDays / monthSummaries.Count * 100;
                monthlyData.RangeCompatibility[range] = compatibility;
            }

            analysis.MonthlyData[monthGroup.Key] = monthlyData;
        }

        // Identify seasonal trends
        analysis.Insights = GenerateSeasonalInsights(analysis.MonthlyData);

        _logger.LogDebug("Completed seasonal analysis across {MonthCount} months", monthlyGroups.Count);

        return analysis;
    }

    /// <inheritdoc />
    public async Task<List<ChallengingDay>> IdentifyChallengingDaysAsync(
        IEnumerable<DailyTripSummary> dailySummaries,
        int evRangeKm)
    {
        var summaries = dailySummaries?.ToList() ?? throw new ArgumentNullException(nameof(dailySummaries));
        var challengingDays = new List<ChallengingDay>();

        foreach (var summary in summaries.Where(s => !s.IsCompatibleWithEVRange(evRangeKm)))
        {
            var excessDistance = summary.LongestTripKm - evRangeKm;
            var severityLevel = CategorizeSeverity(excessDistance);

            var challengingDay = new ChallengingDay
            {
                Date = summary.Date,
                TotalDistance = summary.TotalDistanceKm,
                LongestTrip = summary.LongestTripKm,
                ExcessDistance = excessDistance,
                SeverityLevel = severityLevel,
                NumberOfTrips = summary.MotorizedTrips,
                AverageSpeed = summary.AverageSpeedKmh,
                Recommendations = GenerateChallengingDayRecommendations(summary, evRangeKm)
            };

            challengingDays.Add(challengingDay);
        }

        // Sort by severity and excess distance
        challengingDays = challengingDays
            .OrderByDescending(d => d.SeverityLevel)
            .ThenByDescending(d => d.ExcessDistance)
            .ToList();

        _logger.LogDebug("Identified {ChallengingDayCount} challenging days for {EVRange}km EV range",
            challengingDays.Count, evRangeKm);

        return challengingDays;
    }

    /// <inheritdoc />
    public CompatibilityAnalysisValidation ValidateAnalysisData(IEnumerable<DailyTripSummary> dailySummaries)
    {
        var summaries = dailySummaries?.ToList() ?? throw new ArgumentNullException(nameof(dailySummaries));
        var validation = new CompatibilityAnalysisValidation();

        // Check minimum days for analysis
        if (summaries.Count < MinAnalysisDays)
        {
            validation.Errors.Add($"Insufficient data: {summaries.Count} days available, minimum {MinAnalysisDays} required for reliable analysis");
        }

        // Check for data quality issues
        var daysWithZeroDistance = summaries.Count(s => s.TotalDistanceKm == 0);
        if (daysWithZeroDistance > summaries.Count * 0.5)
        {
            validation.Errors.Add($"High proportion of days with zero distance: {daysWithZeroDistance}/{summaries.Count}");
        }

        // Check for reasonable date range
        if (summaries.Any())
        {
            var dateSpan = summaries.Max(s => s.Date).DayNumber - summaries.Min(s => s.Date).DayNumber;
            if (dateSpan < 7)
            {
                validation.Warnings.Add("Analysis covers less than one week of data");
            }
        }

        // Check for data gaps
        if (summaries.Count > 1)
        {
            var totalSpanDays = summaries.Max(s => s.Date).DayNumber - summaries.Min(s => s.Date).DayNumber + 1;
            var coverage = (double)summaries.Count / totalSpanDays * 100;
            
            if (coverage < 50)
            {
                validation.Warnings.Add($"Low data coverage: {coverage:F1}% of days have driving data");
            }
        }

        validation.IsValid = !validation.Errors.Any();

        return validation;
    }

    /// <summary>
    /// Gets a user-friendly compatibility assessment for a compatibility percentage.
    /// </summary>
    private static string GetCompatibilityAssessment(double compatibilityPercentage)
    {
        return compatibilityPercentage switch
        {
            >= 95 => "Excellent",
            >= 85 => "Very Good",
            >= 70 => "Good",
            >= 50 => "Fair",
            >= 25 => "Limited",
            _ => "Poor"
        };
    }

    /// <summary>
    /// Estimates EV price based on range (rough approximation for comparison).
    /// </summary>
    private static decimal EstimateEVPrice(int rangeKm)
    {
        // Rough price estimation: base price + premium for range
        const decimal basePrice = 30000m;
        const decimal pricePerKm = 100m;
        
        return basePrice + (rangeKm * pricePerKm);
    }

    /// <summary>
    /// Generates notes for a range recommendation.
    /// </summary>
    private string GenerateRangeNotes(double compatibilityPercentage, int range, List<DailyTripSummary> summaries)
    {
        var notes = new List<string>();

        if (compatibilityPercentage >= 95)
        {
            notes.Add("Covers almost all your driving needs");
        }
        else if (compatibilityPercentage >= 85)
        {
            notes.Add("Good coverage for most driving patterns");
        }
        else if (compatibilityPercentage < 70)
        {
            var incompatibleDays = summaries.Count(s => !s.IsCompatibleWithEVRange(range));
            notes.Add($"May require charging planning on {incompatibleDays} days");
        }

        var maxDistance = summaries.Max(s => s.LongestTripKm);
        if (maxDistance > range * 1.5)
        {
            notes.Add("Some trips significantly exceed range");
        }

        return string.Join("; ", notes);
    }

    /// <summary>
    /// Generates seasonal insights from monthly data.
    /// </summary>
    private List<string> GenerateSeasonalInsights(Dictionary<int, MonthlyData> monthlyData)
    {
        var insights = new List<string>();

        if (monthlyData.Count < 3)
        {
            return insights;
        }

        // Find months with highest and lowest driving
        var maxDistanceMonth = monthlyData.OrderByDescending(kvp => kvp.Value.AverageDistance).First();
        var minDistanceMonth = monthlyData.OrderBy(kvp => kvp.Value.AverageDistance).First();

        if (maxDistanceMonth.Value.AverageDistance > minDistanceMonth.Value.AverageDistance * 1.5m)
        {
            insights.Add($"Driving varies significantly by season: {GetMonthName(maxDistanceMonth.Key)} " +
                        $"has {maxDistanceMonth.Value.AverageDistance:F0}km avg vs " +
                        $"{minDistanceMonth.Value.AverageDistance:F0}km in {GetMonthName(minDistanceMonth.Key)}");
        }

        return insights;
    }

    /// <summary>
    /// Gets month name from month number.
    /// </summary>
    private static string GetMonthName(int month)
    {
        return new DateTime(2023, month, 1).ToString("MMMM");
    }

    /// <summary>
    /// Categorizes the severity of a challenging day based on excess distance.
    /// </summary>
    private static ChallengingSeverity CategorizeSeverity(decimal excessDistance)
    {
        return excessDistance switch
        {
            <= 50 => ChallengingSeverity.Minor,
            <= 100 => ChallengingSeverity.Moderate,
            <= 200 => ChallengingSeverity.Major,
            _ => ChallengingSeverity.Severe
        };
    }

    /// <summary>
    /// Generates recommendations for handling a challenging day.
    /// </summary>
    private List<string> GenerateChallengingDayRecommendations(DailyTripSummary summary, int evRangeKm)
    {
        var recommendations = new List<string>();
        var excess = summary.LongestTripKm - evRangeKm;

        if (excess <= 50)
        {
            recommendations.Add("Plan charging stop or use fast charging");
        }
        else if (excess <= 100)
        {
            recommendations.Add("Plan multiple charging stops or consider hybrid vehicle");
        }
        else
        {
            recommendations.Add("Consider alternative transportation or hybrid vehicle for long trips");
        }

        if (summary.MotorizedTrips > 5)
        {
            recommendations.Add("Multiple trips - consider trip consolidation");
        }

        return recommendations;
    }
}

/// <summary>
/// Represents an EV range recommendation with compatibility analysis.
/// </summary>
public class RangeRecommendation
{
    public int EVRangeKm { get; set; }
    public double CompatibilityPercentage { get; set; }
    public int CompatibleDays { get; set; }
    public int TotalDays { get; set; }
    public string Assessment { get; set; } = string.Empty;
    public decimal EstimatedPrice { get; set; }
    public bool IsRecommended { get; set; }
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Represents seasonal driving pattern analysis.
/// </summary>
public class SeasonalAnalysis
{
    public Dictionary<int, MonthlyData> MonthlyData { get; set; } = new();
    public List<string> Insights { get; set; } = new();
}

/// <summary>
/// Represents monthly driving data.
/// </summary>
public class MonthlyData
{
    public int Month { get; set; }
    public int TotalDays { get; set; }
    public decimal AverageDistance { get; set; }
    public decimal MaxDistance { get; set; }
    public int AverageTrips { get; set; }
    public decimal AverageSpeed { get; set; }
    public Dictionary<int, double> RangeCompatibility { get; set; } = new();
}

/// <summary>
/// Represents a challenging day for EV compatibility.
/// </summary>
public class ChallengingDay
{
    public DateOnly Date { get; set; }
    public decimal TotalDistance { get; set; }
    public decimal LongestTrip { get; set; }
    public decimal ExcessDistance { get; set; }
    public ChallengingSeverity SeverityLevel { get; set; }
    public int NumberOfTrips { get; set; }
    public decimal AverageSpeed { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Severity levels for challenging days.
/// </summary>
public enum ChallengingSeverity
{
    Minor = 1,
    Moderate = 2,
    Major = 3,
    Severe = 4
}

/// <summary>
/// Validation result for compatibility analysis data.
/// </summary>
public class CompatibilityAnalysisValidation
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}