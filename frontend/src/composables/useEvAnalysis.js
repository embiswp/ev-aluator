import { ref, computed } from 'vue'

export function useEvAnalysis() {
  const analysisResults = ref(null)
  const isAnalyzing = ref(false)
  const analysisError = ref(null)

  const feasibilityScore = computed(() => {
    return analysisResults.value?.feasibilityScore || 0
  })

  const feasibilityLevel = computed(() => {
    const score = feasibilityScore.value
    if (score >= 90) return 'excellent'
    if (score >= 70) return 'good'
    if (score >= 50) return 'moderate'
    return 'poor'
  })

  const analyzeTrips = (locationData, vehicleConfig) => {
    if (!locationData || locationData.length === 0) {
      throw new Error('No location data available for analysis')
    }

    if (!vehicleConfig || !vehicleConfig.batteryRange) {
      throw new Error('Vehicle configuration is incomplete')
    }

    const trips = extractTripsFromLocations(locationData)
    const analysis = evaluateTripsForEv(trips, vehicleConfig)
    
    return analysis
  }

  const extractTripsFromLocations = (locations) => {
    if (locations.length < 2) return []

    const sortedLocations = [...locations].sort((a, b) => a.timestamp - b.timestamp)
    const trips = []
    let currentTrip = []
    let lastLocation = null

    for (let location of sortedLocations) {
      if (lastLocation) {
        const timeDiff = location.timestamp - lastLocation.timestamp
        const distance = calculateDistance(
          lastLocation.latitude, lastLocation.longitude,
          location.latitude, location.longitude
        )

        if (timeDiff > 30 * 60 * 1000) {
          if (currentTrip.length > 1) {
            trips.push(processTrip(currentTrip))
          }
          currentTrip = [location]
        } else {
          currentTrip.push(location)
        }
      } else {
        currentTrip.push(location)
      }
      
      lastLocation = location
    }

    if (currentTrip.length > 1) {
      trips.push(processTrip(currentTrip))
    }

    return trips.filter(trip => trip.distance > 0.1)
  }

  const processTrip = (locations) => {
    let totalDistance = 0
    
    for (let i = 1; i < locations.length; i++) {
      totalDistance += calculateDistance(
        locations[i-1].latitude, locations[i-1].longitude,
        locations[i].latitude, locations[i].longitude
      )
    }

    return {
      id: Math.random().toString(36).substr(2, 9),
      startTime: locations[0].timestamp,
      endTime: locations[locations.length - 1].timestamp,
      distance: parseFloat(totalDistance.toFixed(2)),
      startLocation: {
        lat: locations[0].latitude,
        lng: locations[0].longitude
      },
      endLocation: {
        lat: locations[locations.length - 1].latitude,
        lng: locations[locations.length - 1].longitude
      },
      duration: (locations[locations.length - 1].timestamp - locations[0].timestamp) / 1000 / 60
    }
  }

  const calculateDistance = (lat1, lon1, lat2, lon2) => {
    const R = 3959
    const dLat = (lat2 - lat1) * Math.PI / 180
    const dLon = (lon2 - lon1) * Math.PI / 180
    const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
              Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
              Math.sin(dLon / 2) * Math.sin(dLon / 2)
    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
    return R * c
  }

  const evaluateTripsForEv = (trips, vehicleConfig) => {
    const { batteryRange } = vehicleConfig
    const safetyMargin = 0.9
    const effectiveRange = batteryRange * safetyMargin

    let feasibleTrips = 0
    const problematicTrips = []
    
    for (let trip of trips) {
      if (trip.distance <= effectiveRange) {
        feasibleTrips++
      } else {
        problematicTrips.push({
          id: trip.id,
          distance: trip.distance,
          reason: trip.distance > batteryRange 
            ? 'Exceeds battery range' 
            : 'Exceeds safe range (90% of battery)'
        })
      }
    }

    const feasibilityScore = trips.length > 0 
      ? Math.round((feasibleTrips / trips.length) * 100)
      : 0

    const maxTripDistance = trips.length > 0 
      ? Math.max(...trips.map(t => t.distance))
      : 0

    const averageTripDistance = trips.length > 0
      ? trips.reduce((sum, trip) => sum + trip.distance, 0) / trips.length
      : 0

    const recommendedRange = Math.max(
      maxTripDistance * 1.2,
      averageTripDistance * 2
    )

    const dailyMileage = calculateDailyMileage(trips)
    const chargingFrequency = calculateChargingFrequency(dailyMileage, batteryRange)

    return {
      feasibilityScore,
      totalTrips: trips.length,
      feasibleTrips,
      averageTripDistance: parseFloat(averageTripDistance.toFixed(1)),
      maxTripDistance: parseFloat(maxTripDistance.toFixed(1)),
      recommendedRange: Math.ceil(recommendedRange),
      chargingFrequency,
      problematicTrips: problematicTrips.slice(0, 10),
      dailyMileage: parseFloat(dailyMileage.toFixed(1))
    }
  }

  const calculateDailyMileage = (trips) => {
    if (trips.length === 0) return 0

    const tripsByDay = new Map()
    
    for (let trip of trips) {
      const day = trip.startTime.toDateString()
      if (!tripsByDay.has(day)) {
        tripsByDay.set(day, 0)
      }
      tripsByDay.set(day, tripsByDay.get(day) + trip.distance)
    }

    const totalMileage = Array.from(tripsByDay.values()).reduce((sum, miles) => sum + miles, 0)
    return totalMileage / tripsByDay.size
  }

  const calculateChargingFrequency = (dailyMileage, batteryRange) => {
    if (dailyMileage === 0) return 'Rarely'
    
    const daysPerCharge = (batteryRange * 0.8) / dailyMileage
    
    if (daysPerCharge >= 7) return 'Weekly'
    if (daysPerCharge >= 3) return '2-3 times per week'
    if (daysPerCharge >= 1) return 'Daily'
    return 'Multiple times daily'
  }

  const performAnalysis = async (locationData, vehicleConfig) => {
    isAnalyzing.value = true
    analysisError.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 1000))
      
      const analysis = analyzeTrips(locationData, vehicleConfig)
      analysisResults.value = analysis
      
      return analysis
    } catch (error) {
      analysisError.value = error.message
      throw error
    } finally {
      isAnalyzing.value = false
    }
  }

  const clearAnalysis = () => {
    analysisResults.value = null
    analysisError.value = null
  }

  return {
    analysisResults,
    isAnalyzing,
    analysisError,
    feasibilityScore,
    feasibilityLevel,
    performAnalysis,
    clearAnalysis
  }
}