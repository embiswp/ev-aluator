import { ref, reactive } from 'vue'

export function useLocationData() {
  const locationData = ref([])
  const isLoading = ref(false)
  const error = ref(null)
  const stats = reactive({
    totalPoints: 0,
    dateRange: null,
    averageAccuracy: 0
  })

  const parseLocationFile = async (file) => {
    isLoading.value = true
    error.value = null

    try {
      const text = await file.text()
      let data = []

      if (file.name.endsWith('.json')) {
        data = parseJsonLocationData(text)
      } else if (file.name.endsWith('.csv')) {
        data = parseCsvLocationData(text)
      } else if (file.name.endsWith('.kml')) {
        data = parseKmlLocationData(text)
      }

      locationData.value = [...locationData.value, ...data]
      updateStats()
      
      return data
    } catch (err) {
      error.value = `Failed to parse ${file.name}: ${err.message}`
      throw err
    } finally {
      isLoading.value = false
    }
  }

  const parseJsonLocationData = (jsonText) => {
    const data = JSON.parse(jsonText)
    
    if (data.locations) {
      return data.locations.map(location => ({
        timestamp: new Date(parseInt(location.timestampMs)),
        latitude: location.latitudeE7 / 10000000,
        longitude: location.longitudeE7 / 10000000,
        accuracy: location.accuracy || null
      }))
    }

    if (Array.isArray(data)) {
      return data.map(point => ({
        timestamp: new Date(point.timestamp),
        latitude: point.latitude || point.lat,
        longitude: point.longitude || point.lng,
        accuracy: point.accuracy || null
      }))
    }

    throw new Error('Unrecognized JSON format')
  }

  const parseCsvLocationData = (csvText) => {
    const lines = csvText.split('\n')
    const headers = lines[0].split(',').map(h => h.trim().toLowerCase())
    
    const latIndex = headers.findIndex(h => h.includes('lat'))
    const lngIndex = headers.findIndex(h => h.includes('lng') || h.includes('lon'))
    const timeIndex = headers.findIndex(h => h.includes('time') || h.includes('date'))
    const accuracyIndex = headers.findIndex(h => h.includes('accuracy'))

    if (latIndex === -1 || lngIndex === -1) {
      throw new Error('CSV must contain latitude and longitude columns')
    }

    return lines.slice(1)
      .filter(line => line.trim())
      .map(line => {
        const values = line.split(',')
        return {
          timestamp: timeIndex !== -1 ? new Date(values[timeIndex]) : new Date(),
          latitude: parseFloat(values[latIndex]),
          longitude: parseFloat(values[lngIndex]),
          accuracy: accuracyIndex !== -1 ? parseFloat(values[accuracyIndex]) : null
        }
      })
      .filter(point => !isNaN(point.latitude) && !isNaN(point.longitude))
  }

  const parseKmlLocationData = (kmlText) => {
    const parser = new DOMParser()
    const xmlDoc = parser.parseFromString(kmlText, 'text/xml')
    const coordinates = xmlDoc.getElementsByTagName('coordinates')
    
    const points = []
    for (let coord of coordinates) {
      const coordText = coord.textContent.trim()
      const coordLines = coordText.split('\n').filter(line => line.trim())
      
      for (let line of coordLines) {
        const parts = line.trim().split(',')
        if (parts.length >= 2) {
          points.push({
            timestamp: new Date(),
            latitude: parseFloat(parts[1]),
            longitude: parseFloat(parts[0]),
            accuracy: null
          })
        }
      }
    }

    if (points.length === 0) {
      throw new Error('No coordinate data found in KML file')
    }

    return points
  }

  const updateStats = () => {
    if (locationData.value.length === 0) return

    stats.totalPoints = locationData.value.length
    
    const timestamps = locationData.value.map(p => p.timestamp).sort()
    stats.dateRange = {
      start: timestamps[0],
      end: timestamps[timestamps.length - 1]
    }

    const accuracyValues = locationData.value
      .map(p => p.accuracy)
      .filter(a => a !== null && !isNaN(a))
    
    stats.averageAccuracy = accuracyValues.length > 0 
      ? accuracyValues.reduce((sum, acc) => sum + acc, 0) / accuracyValues.length
      : 0
  }

  const clearLocationData = () => {
    locationData.value = []
    stats.totalPoints = 0
    stats.dateRange = null
    stats.averageAccuracy = 0
    error.value = null
  }

  return {
    locationData,
    isLoading,
    error,
    stats,
    parseLocationFile,
    clearLocationData
  }
}