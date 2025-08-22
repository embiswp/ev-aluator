export const calculateDistance = (lat1, lon1, lat2, lon2, unit = 'miles') => {
  const R = unit === 'km' ? 6371 : 3959
  
  const dLat = toRadians(lat2 - lat1)
  const dLon = toRadians(lon2 - lon1)
  
  const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
            Math.cos(toRadians(lat1)) * Math.cos(toRadians(lat2)) *
            Math.sin(dLon / 2) * Math.sin(dLon / 2)
  
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
  
  return R * c
}

export const toRadians = (degrees) => {
  return degrees * (Math.PI / 180)
}

export const toDegrees = (radians) => {
  return radians * (180 / Math.PI)
}

export const getBearing = (lat1, lon1, lat2, lon2) => {
  const dLon = toRadians(lon2 - lon1)
  const lat1Rad = toRadians(lat1)
  const lat2Rad = toRadians(lat2)
  
  const y = Math.sin(dLon) * Math.cos(lat2Rad)
  const x = Math.cos(lat1Rad) * Math.sin(lat2Rad) - 
            Math.sin(lat1Rad) * Math.cos(lat2Rad) * Math.cos(dLon)
  
  let bearing = toDegrees(Math.atan2(y, x))
  return (bearing + 360) % 360
}

export const getDestinationPoint = (lat, lon, bearing, distance, unit = 'miles') => {
  const R = unit === 'km' ? 6371 : 3959
  const d = distance / R
  const bearingRad = toRadians(bearing)
  const latRad = toRadians(lat)
  const lonRad = toRadians(lon)
  
  const lat2 = Math.asin(
    Math.sin(latRad) * Math.cos(d) + 
    Math.cos(latRad) * Math.sin(d) * Math.cos(bearingRad)
  )
  
  const lon2 = lonRad + Math.atan2(
    Math.sin(bearingRad) * Math.sin(d) * Math.cos(latRad),
    Math.cos(d) - Math.sin(latRad) * Math.sin(lat2)
  )
  
  return {
    latitude: toDegrees(lat2),
    longitude: toDegrees((lon2 + 3 * Math.PI) % (2 * Math.PI) - Math.PI)
  }
}

export const isPointInRadius = (centerLat, centerLon, pointLat, pointLon, radiusMiles) => {
  const distance = calculateDistance(centerLat, centerLon, pointLat, pointLon, 'miles')
  return distance <= radiusMiles
}

export const formatDistance = (distance, unit = 'miles') => {
  if (distance < 0.1) {
    return unit === 'miles' 
      ? `${Math.round(distance * 5280)} feet`
      : `${Math.round(distance * 1000)} meters`
  }
  
  if (distance < 1) {
    return `${distance.toFixed(2)} ${unit}`
  }
  
  return `${distance.toFixed(1)} ${unit}`
}