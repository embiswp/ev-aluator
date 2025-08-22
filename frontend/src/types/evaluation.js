export const LocationPoint = {
  timestamp: Date,
  latitude: Number,
  longitude: Number,
  accuracy: Number
}

export const Trip = {
  id: String,
  startTime: Date,
  endTime: Date,
  distance: Number,
  duration: Number,
  startLocation: {
    lat: Number,
    lng: Number
  },
  endLocation: {
    lat: Number,
    lng: Number
  }
}

export const VehicleConfiguration = {
  name: String,
  batteryRange: Number,
  chargingSpeed: Number,
  efficiency: Number
}

export const EvaluationResults = {
  feasibilityScore: Number,
  totalTrips: Number,
  feasibleTrips: Number,
  averageTripDistance: Number,
  maxTripDistance: Number,
  recommendedRange: Number,
  chargingFrequency: String,
  dailyMileage: Number,
  problematicTrips: [{
    id: String,
    distance: Number,
    reason: String
  }]
}

export const ChargingStation = {
  id: String,
  name: String,
  latitude: Number,
  longitude: Number,
  address: String,
  connectorTypes: [String],
  maxPowerKw: Number,
  networkName: String,
  isOperational: Boolean,
  pricing: {
    perKwh: Number,
    perMinute: Number,
    sessionFee: Number
  }
}

export const VehiclePreset = {
  id: String,
  make: String,
  model: String,
  year: Number,
  batteryCapacityKwh: Number,
  rangeMiles: Number,
  efficiencyMilesPerKwh: Number,
  maxChargingSpeedKw: Number
}

export const UploadedFile = {
  filename: String,
  id: String,
  recordCount: Number,
  dateRange: {
    start: Date,
    end: Date
  }
}

export const ValidationResult = {
  isValid: Boolean,
  format: String,
  recordCount: Number,
  issues: [String],
  preview: [LocationPoint]
}

export const ApiResponse = {
  success: Boolean,
  data: Object,
  message: String,
  errors: [String]
}

export const LocationStats = {
  totalPoints: Number,
  dateRange: {
    start: Date,
    end: Date
  },
  averageAccuracy: Number
}

export const FeasibilityLevel = {
  EXCELLENT: 'excellent',
  GOOD: 'good', 
  MODERATE: 'moderate',
  POOR: 'poor'
}

export const FileFormat = {
  JSON: 'json',
  CSV: 'csv',
  KML: 'kml'
}

export const ChargingFrequency = {
  RARELY: 'Rarely',
  WEEKLY: 'Weekly',
  SEVERAL_TIMES_WEEKLY: '2-3 times per week',
  DAILY: 'Daily',
  MULTIPLE_DAILY: 'Multiple times daily'
}