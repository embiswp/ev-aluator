namespace EVRangeAnalyzer.Tests.Contract;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

/// <summary>
/// Contract tests for EV analysis endpoints as specified in analysis-api.yaml.
/// These tests verify the API contract compliance for electric vehicle range analysis.
/// </summary>
public class AnalysisApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalysisApiTests"/> class.
    /// </summary>
    /// <param name="factory">The web application factory.</param>
    public AnalysisApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    /// <summary>
    /// Tests POST /analysis/ev-compatibility endpoint contract with valid EV range.
    /// Should return 200 OK with compatibility analysis.
    /// </summary>
    [Fact]
    public async Task PostEvCompatibility_WithValidRange_ShouldReturn200WithAnalysis()
    {
        // Arrange
        await SetupAuthenticatedSessionWithData();
        var request = new EVAnalysisRequest
        {
            EvRangeKm = 400,
            IncludeChargingBuffer = true,
            AnalysisName = "Tesla Model 3 Standard Range",
        };
        var jsonContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/analysis/ev-compatibility", jsonContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var analysisResponse = JsonSerializer.Deserialize<EVAnalysisResponse>(content, _jsonOptions);

        Assert.NotNull(analysisResponse);
        Assert.Equal(400, analysisResponse.EvRangeKm);
        Assert.True(analysisResponse.TotalDaysAnalyzed > 0);
        Assert.True(analysisResponse.CompatibleDays >= 0);
        Assert.True(analysisResponse.IncompatibleDays >= 0);
        Assert.Equal(analysisResponse.TotalDaysAnalyzed, analysisResponse.CompatibleDays + analysisResponse.IncompatibleDays);
        Assert.True(analysisResponse.CompatibilityPercentage >= 0 && analysisResponse.CompatibilityPercentage <= 100);
        Assert.NotNull(analysisResponse.Statistics);
    }

    /// <summary>
    /// Tests POST /analysis/ev-compatibility with invalid EV range (too small).
    /// Should return 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task PostEvCompatibility_WithInvalidRange_ShouldReturn400()
    {
        // Arrange
        await SetupAuthenticatedSessionWithData();
        var request = new EVAnalysisRequest { EvRangeKm = 10, }; // Below minimum of 50
        var jsonContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/analysis/ev-compatibility", jsonContent);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, _jsonOptions);

        Assert.NotNull(errorResponse);
        Assert.Equal("INVALID_EV_RANGE", errorResponse.Error);
        Assert.Contains("50 and 1000 kilometers", errorResponse.Message);
    }

    /// <summary>
    /// Tests POST /analysis/ev-compatibility without location data.
    /// Should return 404 Not Found.
    /// </summary>
    [Fact]
    public async Task PostEvCompatibility_WithoutLocationData_ShouldReturn404()
    {
        // Arrange
        await SetupAuthenticatedSession();
        var request = new EVAnalysisRequest { EvRangeKm = 400, };
        var jsonContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/analysis/ev-compatibility", jsonContent);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, _jsonOptions);

        Assert.NotNull(errorResponse);
        Assert.Equal("NO_LOCATION_DATA", errorResponse.Error);
    }

    /// <summary>
    /// Tests GET /analysis/daily-distances endpoint contract.
    /// Should return 200 OK with daily distance breakdown.
    /// </summary>
    [Fact]
    public async Task GetDailyDistances_WithValidParameters_ShouldReturn200WithDistances()
    {
        // Arrange
        await SetupAuthenticatedSessionWithData();
        var queryParams = "?limit=50&sortBy=distance&sortOrder=desc";

        // Act
        var response = await _client.GetAsync($"/analysis/daily-distances{queryParams}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var dailyDistancesResponse = JsonSerializer.Deserialize<DailyDistancesResponse>(content, _jsonOptions);

        Assert.NotNull(dailyDistancesResponse);
        Assert.NotNull(dailyDistancesResponse.DailyDistances);
        Assert.True(dailyDistancesResponse.TotalDays > 0);
        Assert.NotNull(dailyDistancesResponse.DateRange);
        Assert.True(dailyDistancesResponse.DailyDistances.Count <= 50);

        // Verify sorting (descending by distance)
        if (dailyDistancesResponse.DailyDistances.Count > 1)
        {
            for (int i = 0; i < dailyDistancesResponse.DailyDistances.Count - 1; i++)
            {
                Assert.True(dailyDistancesResponse.DailyDistances[i].TotalDistanceKm >=
                           dailyDistancesResponse.DailyDistances[i + 1].TotalDistanceKm);
            }
        }
    }

    /// <summary>
    /// Tests GET /analysis/daily-distances with date range filter.
    /// Should return 200 OK with filtered results.
    /// </summary>
    [Fact]
    public async Task GetDailyDistances_WithDateFilter_ShouldReturn200WithFilteredResults()
    {
        // Arrange
        await SetupAuthenticatedSessionWithData();
        var startDate = "2024-01-01";
        var endDate = "2024-12-31";
        var queryParams = $"?startDate={startDate}&endDate={endDate}";

        // Act
        var response = await _client.GetAsync($"/analysis/daily-distances{queryParams}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var dailyDistancesResponse = JsonSerializer.Deserialize<DailyDistancesResponse>(content, _jsonOptions);

        Assert.NotNull(dailyDistancesResponse);
        Assert.NotNull(dailyDistancesResponse.DailyDistances);

        // Verify all returned dates are within the specified range
        foreach (var dailyDistance in dailyDistancesResponse.DailyDistances)
        {
            var date = DateOnly.Parse(dailyDistance.Date);
            Assert.True(date >= DateOnly.Parse(startDate));
            Assert.True(date <= DateOnly.Parse(endDate));
        }
    }

    /// <summary>
    /// Tests GET /analysis/statistics endpoint contract.
    /// Should return 200 OK with driving statistics.
    /// </summary>
    [Fact]
    public async Task GetStatistics_WithProcessedData_ShouldReturn200WithStatistics()
    {
        // Arrange
        await SetupAuthenticatedSessionWithData();

        // Act
        var response = await _client.GetAsync("/analysis/statistics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var statistics = JsonSerializer.Deserialize<DrivingStatistics>(content, _jsonOptions);

        Assert.NotNull(statistics);
        Assert.True(statistics.TotalDaysWithDriving > 0);
        Assert.True(statistics.TotalDistanceKm > 0);
        Assert.True(statistics.AverageDailyDistance >= 0);
        Assert.True(statistics.MedianDailyDistance >= 0);
        Assert.True(statistics.MaximumDailyDistance >= statistics.AverageDailyDistance);
        Assert.True(statistics.MinimumDailyDistance <= statistics.AverageDailyDistance);
        Assert.NotNull(statistics.PercentileDistances);
        Assert.NotNull(statistics.TransportModeBreakdown);
    }

    /// <summary>
    /// Tests GET /analysis/ev-recommendations endpoint contract.
    /// Should return 200 OK with EV range recommendations.
    /// </summary>
    [Fact]
    public async Task GetEvRecommendations_WithValidConfidence_ShouldReturn200WithRecommendations()
    {
        // Arrange
        await SetupAuthenticatedSessionWithData();
        var confidenceLevel = 0.95;

        // Act
        var response = await _client.GetAsync($"/analysis/ev-recommendations?confidenceLevel={confidenceLevel}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var recommendationsResponse = JsonSerializer.Deserialize<EVRecommendationsResponse>(content, _jsonOptions);

        Assert.NotNull(recommendationsResponse);
        Assert.Equal(0.95, recommendationsResponse.ConfidenceLevel);
        Assert.NotNull(recommendationsResponse.Recommendations);
        Assert.True(recommendationsResponse.Recommendations.Count > 0);
        Assert.NotNull(recommendationsResponse.Insights);

        // Verify recommendations are sorted by range
        for (int i = 0; i < recommendationsResponse.Recommendations.Count - 1; i++)
        {
            Assert.True(recommendationsResponse.Recommendations[i].RangeKm <=
                       recommendationsResponse.Recommendations[i + 1].RangeKm);
        }
    }

    /// <summary>
    /// Tests GET /analysis/ev-recommendations with invalid confidence level.
    /// Should return 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task GetEvRecommendations_WithInvalidConfidence_ShouldReturn400()
    {
        // Arrange
        await SetupAuthenticatedSessionWithData();
        var invalidConfidence = 1.5; // Above maximum of 0.99

        // Act
        var response = await _client.GetAsync($"/analysis/ev-recommendations?confidenceLevel={invalidConfidence}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests all analysis endpoints without authentication.
    /// Should return 401 Unauthorized.
    /// </summary>
    [Theory]
    [InlineData("POST", "/analysis/ev-compatibility")]
    [InlineData("GET", "/analysis/daily-distances")]
    [InlineData("GET", "/analysis/statistics")]
    [InlineData("GET", "/analysis/ev-recommendations")]
    public async Task AnalysisEndpoints_WithoutAuthentication_ShouldReturn401(string method, string endpoint)
    {
        // Act
        HttpResponseMessage response = method switch
        {
            "POST" => await _client.PostAsync(endpoint, new StringContent("{\"evRangeKm\":400}", Encoding.UTF8, "application/json")),
            "GET" => await _client.GetAsync(endpoint),
            _ => throw new ArgumentException("Unsupported HTTP method")
        };

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tests all analysis endpoints without processed data.
    /// Should return 404 Not Found.
    /// </summary>
    [Theory]
    [InlineData("GET", "/analysis/daily-distances")]
    [InlineData("GET", "/analysis/statistics")]
    [InlineData("GET", "/analysis/ev-recommendations")]
    public async Task AnalysisEndpoints_WithoutProcessedData_ShouldReturn404(string method, string endpoint)
    {
        // Arrange
        await SetupAuthenticatedSession(); // No data uploaded

        // Act
        var response = method switch
        {
            "GET" => await _client.GetAsync(endpoint),
            _ => throw new ArgumentException("Unsupported HTTP method"),
        };

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Sets up an authenticated session for testing purposes.
    /// This will fail until auth implementation is completed (expected in TDD).
    /// </summary>
    private async Task SetupAuthenticatedSession()
    {
        // Mock session setup - will fail until implemented
        _client.DefaultRequestHeaders.Add("Cookie", "ev-session=mock-authenticated-session");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Sets up an authenticated session with processed location data.
    /// This will fail until implementation is completed (expected in TDD).
    /// </summary>
    private async Task SetupAuthenticatedSessionWithData()
    {
        await SetupAuthenticatedSession();

        // Mock data setup - will fail until implemented
        // In actual implementation, would upload and process location data
    }

    // Contract models matching analysis-api.yaml specifications

    /// <summary>
    /// Contract model for EV analysis request.
    /// </summary>
    private class EVAnalysisRequest
    {
        /// <summary>
        /// Gets or sets the electric vehicle range in kilometers.
        /// </summary>
        public int EvRangeKm { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include charging buffer.
        /// </summary>
        public bool IncludeChargingBuffer { get; set; } = true;

        /// <summary>
        /// Gets or sets the optional analysis name.
        /// </summary>
        public string? AnalysisName { get; set; }
    }

    /// <summary>
    /// Contract model for EV analysis response.
    /// </summary>
    private class EVAnalysisResponse
    {
        /// <summary>
        /// Gets or sets the EV range in kilometers.
        /// </summary>
        public int EvRangeKm { get; set; }

        /// <summary>
        /// Gets or sets the total days analyzed.
        /// </summary>
        public int TotalDaysAnalyzed { get; set; }

        /// <summary>
        /// Gets or sets the number of compatible days.
        /// </summary>
        public int CompatibleDays { get; set; }

        /// <summary>
        /// Gets or sets the number of incompatible days.
        /// </summary>
        public int IncompatibleDays { get; set; }

        /// <summary>
        /// Gets or sets the compatibility percentage.
        /// </summary>
        public double CompatibilityPercentage { get; set; }

        /// <summary>
        /// Gets or sets the analysis date.
        /// </summary>
        public DateTime AnalysisDate { get; set; }

        /// <summary>
        /// Gets or sets the statistics.
        /// </summary>
        public AnalysisStatistics Statistics { get; set; } = new AnalysisStatistics();

        /// <summary>
        /// Gets or sets the details of incompatible days.
        /// </summary>
        public List<IncompatibleDayDetail>? IncompatibleDaysDetails { get; set; }
    }

    /// <summary>
    /// Contract model for analysis statistics.
    /// </summary>
    private class AnalysisStatistics
    {
        /// <summary>
        /// Gets or sets the average daily distance.
        /// </summary>
        public double AverageDailyDistance { get; set; }

        /// <summary>
        /// Gets or sets the median daily distance.
        /// </summary>
        public double MedianDailyDistance { get; set; }

        /// <summary>
        /// Gets or sets the maximum daily distance.
        /// </summary>
        public double MaximumDailyDistance { get; set; }

        /// <summary>
        /// Gets or sets the standard deviation.
        /// </summary>
        public double StandardDeviation { get; set; }
    }

    /// <summary>
    /// Contract model for incompatible day details.
    /// </summary>
    private class IncompatibleDayDetail
    {
        /// <summary>
        /// Gets or sets the date.
        /// </summary>
        public string Date { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the distance in kilometers.
        /// </summary>
        public double DistanceKm { get; set; }

        /// <summary>
        /// Gets or sets how much the distance exceeds the range.
        /// </summary>
        public double ExceedsRangeByKm { get; set; }
    }

    /// <summary>
    /// Contract model for daily distances response.
    /// </summary>
    private class DailyDistancesResponse
    {
        /// <summary>
        /// Gets or sets the daily distances.
        /// </summary>
        public List<DailyDistance> DailyDistances { get; set; } = new List<DailyDistance>();

        /// <summary>
        /// Gets or sets the total number of days.
        /// </summary>
        public int TotalDays { get; set; }

        /// <summary>
        /// Gets or sets the date range.
        /// </summary>
        public DateRange DateRange { get; set; } = new DateRange();

        /// <summary>
        /// Gets or sets the pagination information.
        /// </summary>
        public Pagination? Pagination { get; set; }
    }

    /// <summary>
    /// Contract model for daily distance.
    /// </summary>
    private class DailyDistance
    {
        /// <summary>
        /// Gets or sets the date.
        /// </summary>
        public string Date { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total distance in kilometers.
        /// </summary>
        public double TotalDistanceKm { get; set; }

        /// <summary>
        /// Gets or sets the number of motorized trips.
        /// </summary>
        public int MotorizedTrips { get; set; }

        /// <summary>
        /// Gets or sets the longest trip in kilometers.
        /// </summary>
        public double LongestTripKm { get; set; }

        /// <summary>
        /// Gets or sets the average speed in km/h.
        /// </summary>
        public double AverageSpeedKmh { get; set; }

        /// <summary>
        /// Gets or sets the transport modes used.
        /// </summary>
        public List<string> TransportModes { get; set; } = new List<string>();
    }

    /// <summary>
    /// Contract model for date range.
    /// </summary>
    private class DateRange
    {
        /// <summary>
        /// Gets or sets the start date.
        /// </summary>
        public string StartDate { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the end date.
        /// </summary>
        public string EndDate { get; set; } = string.Empty;
    }

    /// <summary>
    /// Contract model for pagination.
    /// </summary>
    private class Pagination
    {
        /// <summary>
        /// Gets or sets a value indicating whether there are more results.
        /// </summary>
        public bool HasMore { get; set; }

        /// <summary>
        /// Gets or sets the next cursor for pagination.
        /// </summary>
        public string? NextCursor { get; set; }
    }

    /// <summary>
    /// Contract model for driving statistics.
    /// </summary>
    private class DrivingStatistics
    {
        /// <summary>
        /// Gets or sets the total days with driving.
        /// </summary>
        public int TotalDaysWithDriving { get; set; }

        /// <summary>
        /// Gets or sets the total distance in kilometers.
        /// </summary>
        public double TotalDistanceKm { get; set; }

        /// <summary>
        /// Gets or sets the average daily distance.
        /// </summary>
        public double AverageDailyDistance { get; set; }

        /// <summary>
        /// Gets or sets the median daily distance.
        /// </summary>
        public double MedianDailyDistance { get; set; }

        /// <summary>
        /// Gets or sets the maximum daily distance.
        /// </summary>
        public double MaximumDailyDistance { get; set; }

        /// <summary>
        /// Gets or sets the minimum daily distance.
        /// </summary>
        public double MinimumDailyDistance { get; set; }

        /// <summary>
        /// Gets or sets the standard deviation.
        /// </summary>
        public double StandardDeviation { get; set; }

        /// <summary>
        /// Gets or sets the percentile distances.
        /// </summary>
        public PercentileDistances PercentileDistances { get; set; } = new PercentileDistances();

        /// <summary>
        /// Gets or sets the transport mode breakdown.
        /// </summary>
        public TransportModeBreakdown TransportModeBreakdown { get; set; } = new TransportModeBreakdown();
    }

    /// <summary>
    /// Contract model for percentile distances.
    /// </summary>
    private class PercentileDistances
    {
        /// <summary>
        /// Gets or sets the 50th percentile distance.
        /// </summary>
        public double P50 { get; set; }

        /// <summary>
        /// Gets or sets the 75th percentile distance.
        /// </summary>
        public double P75 { get; set; }

        /// <summary>
        /// Gets or sets the 90th percentile distance.
        /// </summary>
        public double P90 { get; set; }

        /// <summary>
        /// Gets or sets the 95th percentile distance.
        /// </summary>
        public double P95 { get; set; }

        /// <summary>
        /// Gets or sets the 99th percentile distance.
        /// </summary>
        public double P99 { get; set; }
    }

    /// <summary>
    /// Contract model for transport mode breakdown.
    /// </summary>
    private class TransportModeBreakdown
    {
        /// <summary>
        /// Gets or sets the percentage of distance by vehicle.
        /// </summary>
        public double InVehicle { get; set; }

        /// <summary>
        /// Gets or sets the percentage of distance by bus.
        /// </summary>
        public double InBus { get; set; }

        /// <summary>
        /// Gets or sets the percentage of distance by motorcycle.
        /// </summary>
        public double OnMotorcycle { get; set; }
    }

    /// <summary>
    /// Contract model for EV recommendations response.
    /// </summary>
    private class EVRecommendationsResponse
    {
        /// <summary>
        /// Gets or sets the confidence level.
        /// </summary>
        public double ConfidenceLevel { get; set; }

        /// <summary>
        /// Gets or sets the recommendations.
        /// </summary>
        public List<EVRecommendation> Recommendations { get; set; } = new List<EVRecommendation>();

        /// <summary>
        /// Gets or sets the insights.
        /// </summary>
        public List<string> Insights { get; set; } = new List<string>();
    }

    /// <summary>
    /// Contract model for EV recommendation.
    /// </summary>
    private class EVRecommendation
    {
        /// <summary>
        /// Gets or sets the recommended range in kilometers.
        /// </summary>
        public int RangeKm { get; set; }

        /// <summary>
        /// Gets or sets the compatibility percentage.
        /// </summary>
        public double CompatibilityPercentage { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the recommendation category.
        /// </summary>
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// Contract model for error responses.
    /// </summary>
    private class ErrorResponse
    {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional error details.
        /// </summary>
        public object? Details { get; set; }
    }
}