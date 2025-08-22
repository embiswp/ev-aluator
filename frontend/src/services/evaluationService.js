import apiClient from './api.js'

export const evaluationService = {
  async uploadLocationData(files) {
    const uploadPromises = files.map(file => 
      apiClient.uploadFile('/evaluations/upload-location-data', file)
    )
    
    try {
      const results = await Promise.all(uploadPromises)
      return {
        success: true,
        uploadedFiles: results.map((result, index) => ({
          filename: files[index].name,
          id: result.fileId,
          recordCount: result.recordCount,
          dateRange: result.dateRange
        }))
      }
    } catch (error) {
      throw new Error(`Failed to upload location data: ${error.message}`)
    }
  },

  async createEvaluation(locationDataIds, vehicleConfig) {
    try {
      const response = await apiClient.post('/evaluations', {
        locationDataIds,
        vehicle: {
          name: vehicleConfig.name,
          batteryRangeMiles: vehicleConfig.batteryRange,
          chargingSpeedMilesPerHour: vehicleConfig.chargingSpeed,
          efficiencyMilesPerKwh: vehicleConfig.efficiency
        }
      })

      return response
    } catch (error) {
      throw new Error(`Failed to create evaluation: ${error.message}`)
    }
  },

  async getEvaluationResults(evaluationId) {
    try {
      const response = await apiClient.get(`/evaluations/${evaluationId}`)
      return response
    } catch (error) {
      throw new Error(`Failed to get evaluation results: ${error.message}`)
    }
  },

  async getEvaluationHistory(userId) {
    try {
      const response = await apiClient.get('/evaluations', { userId })
      return response.evaluations || []
    } catch (error) {
      throw new Error(`Failed to get evaluation history: ${error.message}`)
    }
  },

  async deleteEvaluation(evaluationId) {
    try {
      await apiClient.delete(`/evaluations/${evaluationId}`)
      return { success: true }
    } catch (error) {
      throw new Error(`Failed to delete evaluation: ${error.message}`)
    }
  },

  async exportEvaluationReport(evaluationId, format = 'pdf') {
    try {
      const response = await apiClient.get(`/evaluations/${evaluationId}/export`, { format })
      return response
    } catch (error) {
      throw new Error(`Failed to export evaluation report: ${error.message}`)
    }
  },

  async getVehiclePresets() {
    try {
      const response = await apiClient.get('/vehicles/presets')
      return response.vehicles || []
    } catch (error) {
      console.warn('Failed to load vehicle presets:', error.message)
      return []
    }
  },

  async getChargingStations(latitude, longitude, radiusMiles = 50) {
    try {
      const response = await apiClient.get('/charging-stations', {
        lat: latitude,
        lng: longitude,
        radius: radiusMiles
      })
      return response.stations || []
    } catch (error) {
      console.warn('Failed to load charging stations:', error.message)
      return []
    }
  },

  async validateLocationData(file) {
    try {
      const response = await apiClient.uploadFile('/evaluations/validate-location-data', file)
      return {
        isValid: response.isValid,
        format: response.detectedFormat,
        recordCount: response.estimatedRecordCount,
        issues: response.validationIssues || [],
        preview: response.sampleData || []
      }
    } catch (error) {
      throw new Error(`Failed to validate location data: ${error.message}`)
    }
  }
}