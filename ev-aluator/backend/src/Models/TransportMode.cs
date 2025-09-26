namespace EVRangeAnalyzer.Models;

/// <summary>
/// Categorizes transport activities as motorized or non-motorized for EV analysis.
/// Motorized transport is included in EV compatibility calculations.
/// </summary>
public enum TransportCategory
{
    /// <summary>
    /// Vehicle-based transport that would be replaced by an EV (included in analysis).
    /// </summary>
    Motorized,

    /// <summary>
    /// Human-powered, rail, or air transport (excluded from EV analysis).
    /// </summary>
    NonMotorized,
}

/// <summary>
/// Represents different transport modes detected from Google location data.
/// Maps Google's activity types to standardized categories for EV analysis.
/// </summary>
public enum TransportMode
{
    /// <summary>
    /// Car, truck, or other personal vehicle (included in analysis).
    /// Google activity type: "IN_VEHICLE"
    /// </summary>
    InVehicle,

    /// <summary>
    /// Public transit bus (included in analysis).
    /// Google activity type: "IN_BUS"
    /// </summary>
    InBus,

    /// <summary>
    /// Motorcycle or scooter (included in analysis).
    /// Google activity type: "ON_MOTORCYCLE"
    /// </summary>
    OnMotorcycle,

    /// <summary>
    /// Walking on foot (excluded from analysis).
    /// Google activity type: "WALKING"
    /// </summary>
    Walking,

    /// <summary>
    /// Running or jogging (excluded from analysis).
    /// Google activity type: "RUNNING"
    /// </summary>
    Running,

    /// <summary>
    /// Cycling or bicycling (excluded from analysis).
    /// Google activity type: "ON_BICYCLE"
    /// </summary>
    OnBicycle,

    /// <summary>
    /// Train or rail transport (excluded from analysis).
    /// Google activity type: "IN_TRAIN"
    /// </summary>
    InTrain,

    /// <summary>
    /// Air travel (excluded from analysis).
    /// Google activity type: "IN_FLIGHT"
    /// </summary>
    InFlight,

    /// <summary>
    /// Unclassified or unknown activity (excluded from analysis).
    /// Google activity type: "UNKNOWN" or missing
    /// </summary>
    Unknown,
}

/// <summary>
/// Processing status for uploaded location history files.
/// Tracks the lifecycle from upload through parsing to analysis readiness.
/// </summary>
public enum ProcessingStatus
{
    /// <summary>
    /// File transfer is in progress.
    /// </summary>
    Uploading,

    /// <summary>
    /// JSON deserialization and validation is active.
    /// </summary>
    Parsing,

    /// <summary>
    /// Location point analysis and trip calculation is in progress.
    /// </summary>
    Processing,

    /// <summary>
    /// Processing completed successfully, ready for EV analysis.
    /// </summary>
    Completed,

    /// <summary>
    /// An error occurred during processing.
    /// </summary>
    Failed,
}

/// <summary>
/// Provides utility methods for working with transport modes and Google activity mappings.
/// </summary>
public static class TransportModeExtensions
{
    /// <summary>
    /// Maps Google activity type strings to TransportMode enum values.
    /// </summary>
    private static readonly Dictionary<string, TransportMode> GoogleActivityMapping = new()
    {
        { "IN_VEHICLE", TransportMode.InVehicle },
        { "IN_BUS", TransportMode.InBus },
        { "ON_MOTORCYCLE", TransportMode.OnMotorcycle },
        { "WALKING", TransportMode.Walking },
        { "RUNNING", TransportMode.Running },
        { "ON_BICYCLE", TransportMode.OnBicycle },
        { "IN_TRAIN", TransportMode.InTrain },
        { "IN_FLIGHT", TransportMode.InFlight },
        { "UNKNOWN", TransportMode.Unknown },
    };

    /// <summary>
    /// Transport modes that are included in EV compatibility analysis.
    /// These represent motorized transport that could be replaced by an electric vehicle.
    /// </summary>
    private static readonly HashSet<TransportMode> MotorizedModes = new()
    {
        TransportMode.InVehicle,
        TransportMode.InBus,
        TransportMode.OnMotorcycle,
    };

    /// <summary>
    /// Expected speed ranges for different transport modes in km/h.
    /// Used for validation and anomaly detection.
    /// </summary>
    private static readonly Dictionary<TransportMode, (int Min, int Max)> TypicalSpeedRanges = new()
    {
        { TransportMode.InVehicle, (0, 120) },
        { TransportMode.InBus, (0, 80) },
        { TransportMode.OnMotorcycle, (0, 120) },
        { TransportMode.Walking, (0, 8) },
        { TransportMode.Running, (5, 25) },
        { TransportMode.OnBicycle, (0, 50) },
        { TransportMode.InTrain, (0, 300) },
        { TransportMode.InFlight, (100, 900) },
        { TransportMode.Unknown, (0, 200) },
    };

    /// <summary>
    /// Converts a Google activity type string to a TransportMode enum value.
    /// </summary>
    /// <param name="googleActivityType">The Google activity type string (e.g., "IN_VEHICLE").</param>
    /// <returns>The corresponding TransportMode, or Unknown if not recognized.</returns>
    public static TransportMode FromGoogleActivityType(string? googleActivityType)
    {
        if (string.IsNullOrWhiteSpace(googleActivityType))
        {
            return TransportMode.Unknown;
        }

        return GoogleActivityMapping.GetValueOrDefault(googleActivityType.ToUpperInvariant(), TransportMode.Unknown);
    }

    /// <summary>
    /// Gets the transport category for a given transport mode.
    /// </summary>
    /// <param name="mode">The transport mode to categorize.</param>
    /// <returns>The transport category (Motorized or NonMotorized).</returns>
    public static TransportCategory GetCategory(this TransportMode mode)
    {
        return MotorizedModes.Contains(mode) ? TransportCategory.Motorized : TransportCategory.NonMotorized;
    }

    /// <summary>
    /// Determines if a transport mode should be included in EV compatibility analysis.
    /// </summary>
    /// <param name="mode">The transport mode to check.</param>
    /// <returns>True if the mode represents motorized transport that could be replaced by an EV.</returns>
    public static bool IsIncludedInAnalysis(this TransportMode mode)
    {
        return mode.GetCategory() == TransportCategory.Motorized;
    }

    /// <summary>
    /// Gets the human-readable display name for a transport mode.
    /// </summary>
    /// <param name="mode">The transport mode.</param>
    /// <returns>A user-friendly display name.</returns>
    public static string GetDisplayName(this TransportMode mode)
    {
        return mode switch
        {
            TransportMode.InVehicle => "In Vehicle",
            TransportMode.InBus => "In Bus",
            TransportMode.OnMotorcycle => "On Motorcycle",
            TransportMode.Walking => "Walking",
            TransportMode.Running => "Running",
            TransportMode.OnBicycle => "On Bicycle",
            TransportMode.InTrain => "In Train",
            TransportMode.InFlight => "In Flight",
            TransportMode.Unknown => "Unknown",
            _ => mode.ToString(),
        };
    }

    /// <summary>
    /// Gets the typical speed range for a transport mode in km/h.
    /// Used for validation and anomaly detection.
    /// </summary>
    /// <param name="mode">The transport mode.</param>
    /// <returns>A tuple containing the minimum and maximum expected speeds.</returns>
    public static (int Min, int Max) GetTypicalSpeedRange(this TransportMode mode)
    {
        return TypicalSpeedRanges.GetValueOrDefault(mode, (0, 200));
    }

    /// <summary>
    /// Validates if a speed is within the typical range for a transport mode.
    /// </summary>
    /// <param name="mode">The transport mode.</param>
    /// <param name="speedKmh">The speed to validate in km/h.</param>
    /// <returns>True if the speed is within the expected range for the mode.</returns>
    public static bool IsSpeedValid(this TransportMode mode, double speedKmh)
    {
        var (min, max) = mode.GetTypicalSpeedRange();
        return speedKmh >= min && speedKmh <= max;
    }

    /// <summary>
    /// Gets all motorized transport modes that are included in EV analysis.
    /// </summary>
    /// <returns>An enumerable of motorized transport modes.</returns>
    public static IEnumerable<TransportMode> GetMotorizedModes()
    {
        return MotorizedModes.AsEnumerable();
    }

    /// <summary>
    /// Gets all available Google activity type mappings.
    /// </summary>
    /// <returns>A dictionary mapping Google activity types to TransportMode enum values.</returns>
    public static IReadOnlyDictionary<string, TransportMode> GetGoogleActivityMappings()
    {
        return GoogleActivityMapping.AsReadOnly();
    }
}