export const isValidLatitude = (lat) => {
  return typeof lat === 'number' && lat >= -90 && lat <= 90
}

export const isValidLongitude = (lng) => {
  return typeof lng === 'number' && lng >= -180 && lng <= 180
}

export const isValidCoordinate = (lat, lng) => {
  return isValidLatitude(lat) && isValidLongitude(lng)
}

export const isValidRange = (range) => {
  return typeof range === 'number' && range > 0 && range <= 1000
}

export const isValidChargingSpeed = (speed) => {
  return typeof speed === 'number' && speed > 0 && speed <= 1000
}

export const isValidEfficiency = (efficiency) => {
  return typeof efficiency === 'number' && efficiency > 0 && efficiency <= 10
}

export const isValidVehicleConfig = (config) => {
  if (!config || typeof config !== 'object') {
    return { isValid: false, errors: ['Vehicle configuration is required'] }
  }

  const errors = []

  if (!config.name || typeof config.name !== 'string' || config.name.trim().length === 0) {
    errors.push('Vehicle name is required')
  }

  if (!isValidRange(config.batteryRange)) {
    errors.push('Battery range must be a number between 1 and 1000 miles')
  }

  if (config.chargingSpeed && !isValidChargingSpeed(config.chargingSpeed)) {
    errors.push('Charging speed must be a number between 1 and 1000 miles per hour')
  }

  if (config.efficiency && !isValidEfficiency(config.efficiency)) {
    errors.push('Efficiency must be a number between 0.1 and 10 miles per kWh')
  }

  return {
    isValid: errors.length === 0,
    errors
  }
}

export const isValidLocationPoint = (point) => {
  if (!point || typeof point !== 'object') {
    return false
  }

  if (!point.timestamp || !(point.timestamp instanceof Date)) {
    return false
  }

  if (!isValidCoordinate(point.latitude, point.longitude)) {
    return false
  }

  if (point.accuracy !== null && point.accuracy !== undefined) {
    if (typeof point.accuracy !== 'number' || point.accuracy < 0) {
      return false
    }
  }

  return true
}

export const isValidLocationData = (data) => {
  if (!Array.isArray(data) || data.length === 0) {
    return { isValid: false, errors: ['Location data must be a non-empty array'] }
  }

  const errors = []
  const invalidPoints = []

  data.forEach((point, index) => {
    if (!isValidLocationPoint(point)) {
      invalidPoints.push(index)
    }
  })

  if (invalidPoints.length > 0) {
    if (invalidPoints.length === data.length) {
      errors.push('All location points are invalid')
    } else if (invalidPoints.length > data.length * 0.1) {
      errors.push(`Too many invalid points: ${invalidPoints.length}/${data.length}`)
    } else {
      errors.push(`Some invalid points found: ${invalidPoints.length}/${data.length}`)
    }
  }

  const validPointCount = data.length - invalidPoints.length
  if (validPointCount < 2) {
    errors.push('At least 2 valid location points are required for analysis')
  }

  return {
    isValid: errors.length === 0 && validPointCount >= 2,
    errors,
    validPointCount,
    invalidPointCount: invalidPoints.length
  }
}

export const isValidEmail = (email) => {
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  return emailRegex.test(email)
}

export const isValidPhone = (phone) => {
  const phoneRegex = /^\+?[\d\s\-\(\)]{10,}$/
  return phoneRegex.test(phone)
}

export const isValidUrl = (url) => {
  try {
    new URL(url)
    return true
  } catch {
    return false
  }
}

export const isValidFileSize = (file, maxSizeMB = 50) => {
  const maxSizeBytes = maxSizeMB * 1024 * 1024
  return file.size <= maxSizeBytes
}

export const isValidFileType = (file, allowedTypes = []) => {
  if (allowedTypes.length === 0) return true
  
  const fileExtension = file.name.split('.').pop().toLowerCase()
  return allowedTypes.includes(fileExtension)
}

export const sanitizeFileName = (fileName) => {
  return fileName.replace(/[^a-z0-9.-]/gi, '_').toLowerCase()
}

export const validateDateRange = (startDate, endDate) => {
  if (!(startDate instanceof Date) || !(endDate instanceof Date)) {
    return { isValid: false, error: 'Dates must be valid Date objects' }
  }

  if (startDate >= endDate) {
    return { isValid: false, error: 'Start date must be before end date' }
  }

  const now = new Date()
  if (endDate > now) {
    return { isValid: false, error: 'End date cannot be in the future' }
  }

  const maxRangeDays = 365 * 5
  const rangeDays = (endDate - startDate) / (1000 * 60 * 60 * 24)
  if (rangeDays > maxRangeDays) {
    return { isValid: false, error: 'Date range cannot exceed 5 years' }
  }

  return { isValid: true }
}