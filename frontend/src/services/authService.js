import apiClient from './api.js'

export const authService = {
  async googleSignIn(idToken) {
    try {
      const response = await apiClient.post('/auth/google-signin', { idToken })
      return {
        success: true,
        user: response.user,
        expiresAt: response.expiresAt
      }
    } catch (error) {
      throw new Error(`Google sign-in failed: ${error.message}`)
    }
  },

  async getUserProfile() {
    try {
      const response = await apiClient.get('/auth/profile')
      return response.user
    } catch (error) {
      throw new Error(`Failed to get user profile: ${error.message}`)
    }
  },

  async signOut() {
    try {
      await apiClient.post('/auth/signout')
      return { success: true }
    } catch (error) {
      console.warn('Sign out API call failed:', error.message)
      return { success: true }
    }
  },

  async refreshToken() {
    try {
      const response = await apiClient.post('/auth/refresh-token')
      return {
        success: true,
        expiresAt: response.expiresAt
      }
    } catch (error) {
      throw new Error(`Token refresh failed: ${error.message}`)
    }
  }
}