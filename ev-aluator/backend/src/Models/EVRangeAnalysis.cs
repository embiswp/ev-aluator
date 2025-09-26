using System.ComponentModel.DataAnnotations;

namespace EVRangeAnalyzer.Models;

/// <summary>
/// Represents the results of EV range compatibility analysis based on historical driving data.
/// Generated on-demand when user specifies an EV range and requests compatibility assessment.
/// Contains comprehensive metrics about driving pattern compatibility with electric vehicles.
/// </summary>
public class EVRangeAnalysis
{
    /// <summary>
    /// Gets or sets the session ID that owns this analysis.
    /// Foreign key reference to UserSession for data isolation.
    /// </summary>
    [Required]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-specified EV range in kilometers.
    /// This is the single-charge driving range the user wants to analyze against their data.
    /// </summary>
    [Required]
    [Range(1, 1000, ErrorMessage = "EV range must be between 1 and 1000 kilometers")]
    public int EVRangeKm { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this analysis was performed (UTC).
    /// Used for cache management and result freshness validation.
    /// </summary>
    [Required]
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the total number of driving days analyzed from the location history.
    /// Represents days with meaningful motorized vehicle activity.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Total days analyzed must be non-negative")]
    public int TotalDaysAnalyzed { get; set; }

    /// <summary>
    /// Gets or sets the number of days where all trips were within the EV's range.
    /// These days represent full compatibility with the specified EV.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Compatible days must be non-negative")]
    public int CompatibleDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days where at least one trip exceeded the EV's range.
    /// These days would require charging stops or alternative transportation.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Incompatible days must be non-negative")]
    public int IncompatibleDays { get; set; }

    /// <summary>
    /// Gets or sets the percentage of days that are fully compatible with the EV range.
    /// Calculated as (CompatibleDays / TotalDaysAnalyzed) * 100.
    /// </summary>
    [Range(0, 100, ErrorMessage = "Compatibility percentage must be between 0 and 100")]
    public decimal CompatibilityPercentage { get; set; }

    /// <summary>
    /// Gets or sets the mean daily driving distance across all analyzed days.
    /// Provides context about typical daily driving patterns.
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Average daily distance must be non-negative")]
    public decimal AverageDailyDistance { get; set; }

    /// <summary>
    /// Gets or sets the highest single-day driving distance in the dataset.
    /// Represents the most challenging day for EV range requirements.
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Maximum daily distance must be non-negative")]
    public decimal MaximumDailyDistance { get; set; }

    /// <summary>
    /// Gets or sets the EV range required to achieve 95% compatibility with historical data.
    /// Useful for EV selection guidance - covers 95% of historical driving days.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Recommended minimum range must be non-negative")]
    public int RecommendedMinimumRange { get; set; }

    /// <summary>
    /// Gets or sets the EV range required to achieve 100% compatibility.
    /// This would handle even the longest driving days without charging stops.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Required full compatibility range must be non-negative")]
    public int RequiredFullCompatibilityRange { get; set; }

    /// <summary>
    /// Gets or sets the date range of the analyzed location data.
    /// Shows the time period covered by this analysis.
    /// </summary>
    public (DateTime Start, DateTime End) AnalysisDateRange { get; set; }

    /// <summary>
    /// Gets or sets detailed breakdown of incompatible days by distance ranges.
    /// Helps understand the severity of range limitations.
    /// </summary>
    public Dictionary<string, int> IncompatibilityBreakdown { get; set; } = new();

    /// <summary>
    /// Gets or sets seasonal analysis showing compatibility by month.
    /// Helps identify if certain times of year are more challenging for EVs.
    /// </summary>
    public Dictionary<int, decimal> MonthlyCompatibility { get; set; } = new();

    /// <summary>
    /// Gets or sets the transport modes that were included in this analysis.
    /// Shows what types of motorized transport contributed to the results.
    /// </summary>
    public List<TransportMode> AnalyzedTransportModes { get; set; } = new();

    /// <summary>
    /// Gets or sets additional analysis metadata and processing information.
    /// Contains data quality metrics, calculation methods, and warnings.
    /// </summary>
    public Dictionary<string, object> AnalysisMetadata { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EVRangeAnalysis"/> class.
    /// </summary>
    public EVRangeAnalysis()
    {
        AnalysisDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new EVRangeAnalysis from daily trip summaries and specified EV range.
    /// </summary>
    /// <param name="sessionId">The session ID that owns this analysis.</param>
    /// <param name="evRangeKm">The EV range to analyze against.</param>
    /// <param name="dailySummaries">Collection of daily trip summaries to analyze.</param>
    /// <returns>A comprehensive EV range compatibility analysis.</returns>
    public static EVRangeAnalysis CreateFromDailySummaries(
        string sessionId,
        int evRangeKm,
        IEnumerable<DailyTripSummary> dailySummaries)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        if (evRangeKm <= 0 || evRangeKm > 1000)
            throw new ArgumentException("EV range must be between 1 and 1000 kilometers", nameof(evRangeKm));

        var summaries = dailySummaries?.Where(s => s.IsSignificantDrivingDay()).ToList()
                       ?? throw new ArgumentNullException(nameof(dailySummaries));

        var analysis = new EVRangeAnalysis
        {
            SessionId = sessionId,
            EVRangeKm = evRangeKm,
            TotalDaysAnalyzed = summaries.Count,
        };

        if (!summaries.Any())
        {
            // Return empty analysis for datasets with no driving days
            analysis.AnalysisMetadata["Warning"] = "No significant driving days found in dataset";
            return analysis;
        }

        // Calculate basic compatibility metrics
        analysis.CompatibleDays = summaries.Count(s => s.IsCompatibleWithEVRange(evRangeKm));
        analysis.IncompatibleDays = analysis.TotalDaysAnalyzed - analysis.CompatibleDays;
        analysis.CompatibilityPercentage = analysis.TotalDaysAnalyzed > 0
            ? (analysis.CompatibleDays * 100.0m) / analysis.TotalDaysAnalyzed
            : 0;

        // Calculate distance statistics
        analysis.AverageDailyDistance = summaries.Average(s => s.TotalDistanceKm);
        analysis.MaximumDailyDistance = summaries.Max(s => s.TotalDistanceKm);

        // Calculate range recommendations
        var longestTrips = summaries.Select(s => s.LongestTripKm).OrderBy(d => d).ToList();
        analysis.RecommendedMinimumRange = CalculatePercentileRange(longestTrips, 0.95);
        analysis.RequiredFullCompatibilityRange = (int)Math.Ceiling(longestTrips.Last());

        // Set analysis date range
        analysis.AnalysisDateRange = (
            summaries.Min(s => s.Date).ToDateTime(TimeOnly.MinValue),
            summaries.Max(s => s.Date).ToDateTime(TimeOnly.MinValue)
        );

        // Calculate incompatibility breakdown
        analysis.IncompatibilityBreakdown = CalculateIncompatibilityBreakdown(summaries, evRangeKm);

        // Calculate monthly compatibility
        analysis.MonthlyCompatibility = CalculateMonthlyCompatibility(summaries, evRangeKm);

        // Extract analyzed transport modes
        analysis.AnalyzedTransportModes = summaries.SelectMany(s => s.TransportModes)
                                                  .Distinct()
                                                  .ToList();

        // Store metadata
        analysis.AnalysisMetadata["DataQualityScore"] = CalculateOverallDataQuality(summaries);
        analysis.AnalysisMetadata["AnalysisMethod"] = "LongestTripPerDay";
        analysis.AnalysisMetadata["ProcessedDays"] = summaries.Count;
        analysis.AnalysisMetadata["TotalTrips"] = summaries.Sum(s => s.MotorizedTrips);

        return analysis;
    }

    /// <summary>
    /// Validates the current EV range analysis for logical consistency.
    /// </summary>
    /// <returns>A list of validation error messages, or empty list if valid.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        // Required field validation
        if (string.IsNullOrWhiteSpace(SessionId))
            errors.Add("Session ID is required");

        // Range validation
        if (EVRangeKm <= 0 || EVRangeKm > 1000)
            errors.Add("EV range must be between 1 and 1000 kilometers");

        // Count validations
        if (TotalDaysAnalyzed < 0)
            errors.Add("Total days analyzed must be non-negative");

        if (CompatibleDays < 0)
            errors.Add("Compatible days must be non-negative");

        if (IncompatibleDays < 0)
            errors.Add("Incompatible days must be non-negative");

        // Logical consistency checks
        if (CompatibleDays + IncompatibleDays != TotalDaysAnalyzed)
            errors.Add("Compatible days plus incompatible days must equal total days analyzed");

        // Percentage validation
        if (CompatibilityPercentage < 0 || CompatibilityPercentage > 100)
            errors.Add("Compatibility percentage must be between 0 and 100");

        // Distance validations
        if (AverageDailyDistance < 0)
            errors.Add("Average daily distance must be non-negative");

        if (MaximumDailyDistance < 0)
            errors.Add("Maximum daily distance must be non-negative");

        if (MaximumDailyDistance < AverageDailyDistance)
            errors.Add("Maximum daily distance cannot be less than average daily distance");

        // Range recommendation validations
        if (RecommendedMinimumRange < 0)
            errors.Add("Recommended minimum range must be non-negative");

        if (RequiredFullCompatibilityRange < 0)
            errors.Add("Required full compatibility range must be non-negative");

        if (RecommendedMinimumRange > RequiredFullCompatibilityRange)
            errors.Add("Recommended minimum range cannot exceed required full compatibility range");

        // Date range validation
        if (AnalysisDateRange.Start > AnalysisDateRange.End)
            errors.Add("Analysis date range start must be before or equal to end");

        return errors;
    }

    /// <summary>
    /// Gets a user-friendly compatibility assessment based on the analysis results.
    /// </summary>
    /// <returns>A string describing the EV compatibility level.</returns>
    public string GetCompatibilityAssessment()
    {
        return CompatibilityPercentage switch
        {
            >= 95 => "Excellent - This EV range handles almost all your driving needs",
            >= 85 => "Very Good - This EV range works well for most of your driving",
            >= 70 => "Good - This EV range covers most days, with occasional charging needs",
            >= 50 => "Fair - This EV range requires planning for longer trips",
            >= 25 => "Limited - This EV range works for short trips but often needs charging",
            _ => "Poor - This EV range is insufficient for your typical driving patterns"
        };
    }

    /// <summary>
    /// Gets recommendations for improving EV compatibility.
    /// </summary>
    /// <returns>A list of actionable recommendations.</returns>
    public List<string> GetRecommendations()
    {
        var recommendations = new List<string>();

        if (CompatibilityPercentage < 80)
        {
            recommendations.Add($"Consider an EV with at least {RecommendedMinimumRange}km range for 95% compatibility");
        }

        if (IncompatibleDays > 0)
        {
            recommendations.Add("Plan charging stops for longer trips or use alternative transport");
        }

        if (MaximumDailyDistance > EVRangeKm * 2)
        {
            recommendations.Add("Longest trips may require multiple charging stops or hybrid vehicle");
        }

        if (MonthlyCompatibility.Any() && MonthlyCompatibility.Values.Max() - MonthlyCompatibility.Values.Min() > 20)
        {
            recommendations.Add("Consider seasonal driving patterns when planning EV usage");
        }

        return recommendations;
    }

    /// <summary>
    /// Creates a summary string for display and logging purposes.
    /// </summary>
    /// <returns>A human-readable summary of the analysis results.</returns>
    public override string ToString()
    {
        return $"EV Range Analysis: {EVRangeKm}km range achieves {CompatibilityPercentage:F1}% compatibility " +
               $"({CompatibleDays}/{TotalDaysAnalyzed} days) with avg daily distance {AverageDailyDistance:F1}km";
    }

    /// <summary>
    /// Calculates the range needed to achieve a specific percentile of trip compatibility.
    /// </summary>
    /// <param name="longestTrips">List of longest daily trips, sorted ascending.</param>
    /// <param name="percentile">Percentile to calculate (0.0 to 1.0).</param>
    /// <returns>Range in kilometers needed for the specified percentile coverage.</returns>
    private static int CalculatePercentileRange(List<decimal> longestTrips, double percentile)
    {
        if (!longestTrips.Any()) return 0;
        
        var index = (int)Math.Ceiling(longestTrips.Count * percentile) - 1;
        index = Math.Max(0, Math.Min(index, longestTrips.Count - 1));
        
        return (int)Math.Ceiling(longestTrips[index]);
    }

    /// <summary>
    /// Calculates breakdown of incompatible days by excess distance ranges.
    /// </summary>
    /// <param name="summaries">Daily trip summaries to analyze.</param>
    /// <param name="evRangeKm">EV range being analyzed.</param>
    /// <returns>Dictionary mapping distance ranges to day counts.</returns>
    private static Dictionary<string, int> CalculateIncompatibilityBreakdown(
        List<DailyTripSummary> summaries, 
        int evRangeKm)
    {
        var breakdown = new Dictionary<string, int>
        {
            ["0-50km over"] = 0,
            ["50-100km over"] = 0,
            ["100-200km over"] = 0,
            ["200km+ over"] = 0,
        };

        foreach (var summary in summaries.Where(s => !s.IsCompatibleWithEVRange(evRangeKm)))
        {
            var excess = summary.LongestTripKm - evRangeKm;
            
            var category = excess switch
            {
                <= 50 => "0-50km over",
                <= 100 => "50-100km over",
                <= 200 => "100-200km over",
                _ => "200km+ over",
            };
            
            breakdown[category]++;
        }

        return breakdown;
    }

    /// <summary>
    /// Calculates monthly compatibility percentages.
    /// </summary>
    /// <param name="summaries">Daily trip summaries to analyze.</param>
    /// <param name="evRangeKm">EV range being analyzed.</param>
    /// <returns>Dictionary mapping month numbers to compatibility percentages.</returns>
    private static Dictionary<int, decimal> CalculateMonthlyCompatibility(
        List<DailyTripSummary> summaries,
        int evRangeKm)
    {
        return summaries.GroupBy(s => s.Date.Month)
                       .ToDictionary(
                           g => g.Key,
                           g => g.Count() > 0 
                               ? (g.Count(s => s.IsCompatibleWithEVRange(evRangeKm)) * 100.0m) / g.Count()
                               : 0m
                       );
    }

    /// <summary>
    /// Calculates overall data quality score from daily summaries.
    /// </summary>
    /// <param name="summaries">Daily trip summaries to analyze.</param>
    /// <returns>Data quality score between 0 and 1.</returns>
    private static double CalculateOverallDataQuality(List<DailyTripSummary> summaries)
    {
        if (!summaries.Any()) return 0.0;

        var totalQuality = summaries.Sum(s => 
        {
            var hasMetadata = s.CalculationMetadata.ContainsKey("DataQuality");
            return hasMetadata ? Convert.ToDouble(s.CalculationMetadata["DataQuality"]) : 0.5;
        });

        return totalQuality / summaries.Count;
    }
}