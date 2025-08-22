export const formatFileSize = (bytes) => {
  if (bytes === 0) return '0 Bytes'
  
  const k = 1024
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`
}

export const formatDate = (date, options = {}) => {
  const defaultOptions = {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  }
  
  return new Intl.DateTimeFormat('en-US', { ...defaultOptions, ...options }).format(date)
}

export const formatDateTime = (date, options = {}) => {
  const defaultOptions = {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }
  
  return new Intl.DateTimeFormat('en-US', { ...defaultOptions, ...options }).format(date)
}

export const formatDuration = (minutes) => {
  if (minutes < 60) {
    return `${Math.round(minutes)} min`
  }
  
  const hours = Math.floor(minutes / 60)
  const remainingMinutes = Math.round(minutes % 60)
  
  if (hours < 24) {
    return remainingMinutes > 0 
      ? `${hours}h ${remainingMinutes}m`
      : `${hours}h`
  }
  
  const days = Math.floor(hours / 24)
  const remainingHours = hours % 24
  
  let result = `${days}d`
  if (remainingHours > 0) {
    result += ` ${remainingHours}h`
  }
  
  return result
}

export const formatNumber = (number, decimals = 0) => {
  return new Intl.NumberFormat('en-US', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals
  }).format(number)
}

export const formatCurrency = (amount, currency = 'USD') => {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: currency
  }).format(amount)
}

export const formatPercentage = (value, decimals = 0) => {
  return new Intl.NumberFormat('en-US', {
    style: 'percent',
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals
  }).format(value / 100)
}

export const formatCoordinate = (coordinate, precision = 6) => {
  return parseFloat(coordinate.toFixed(precision))
}

export const formatSpeed = (milesPerHour) => {
  if (milesPerHour < 1) {
    return '< 1 mph'
  }
  
  return `${Math.round(milesPerHour)} mph`
}

export const formatEnergy = (kWh) => {
  if (kWh < 1) {
    return `${Math.round(kWh * 1000)} Wh`
  }
  
  return `${kWh.toFixed(1)} kWh`
}

export const formatPower = (kW) => {
  if (kW < 1) {
    return `${Math.round(kW * 1000)} W`
  }
  
  return `${kW.toFixed(1)} kW`
}

export const formatEfficiency = (milesPerKwh) => {
  return `${milesPerKwh.toFixed(1)} mi/kWh`
}

export const formatRange = (miles) => {
  if (miles < 1) {
    return '< 1 mile'
  }
  
  return `${Math.round(miles)} miles`
}

export const truncateText = (text, maxLength) => {
  if (text.length <= maxLength) {
    return text
  }
  
  return text.substring(0, maxLength - 3) + '...'
}

export const capitalizeFirst = (text) => {
  return text.charAt(0).toUpperCase() + text.slice(1).toLowerCase()
}

export const formatPhoneNumber = (phone) => {
  const cleaned = phone.replace(/\D/g, '')
  
  if (cleaned.length === 10) {
    return `(${cleaned.slice(0, 3)}) ${cleaned.slice(3, 6)}-${cleaned.slice(6)}`
  }
  
  return phone
}