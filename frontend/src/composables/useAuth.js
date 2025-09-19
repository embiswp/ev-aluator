import { ref, computed, onMounted, onUnmounted } from 'vue'
import { tokenManager } from '../services/tokenManager.js'
import { authService } from '../services/authService.js'

const isAuthenticated = ref(false)
const user = ref(null)
const isLoading = ref(false)
const error = ref(null)

let isInitialized = false

export function useAuth() {
  const initialize = async () => {
    if (isInitialized) return
    isInitialized = true

    isLoading.value = true
    error.value = null

    try {
      tokenManager.initialize()

      if (tokenManager.hasValidToken()) {
        const tokenUser = tokenManager.getUserFromToken()
        if (tokenUser) {
          user.value = tokenUser
          isAuthenticated.value = true

          try {
            const profileUser = await authService.getUserProfile()
            user.value = profileUser
          } catch (profileError) {
            console.warn('Failed to fetch fresh user profile:', profileError.message)
          }
        }
      }
    } catch (err) {
      error.value = err.message
      await signOut()
    } finally {
      isLoading.value = false
    }
  }

  const signIn = async (idToken) => {
    isLoading.value = true
    error.value = null

    try {
      const result = await authService.googleSignIn(idToken)
      
      user.value = result.user
      isAuthenticated.value = true
      
      tokenManager.setTokens(
        result.accessToken, 
        result.refreshToken, 
        result.expiresAt
      )

      return result
    } catch (err) {
      error.value = err.message
      throw err
    } finally {
      isLoading.value = false
    }
  }

  const signOut = async () => {
    isLoading.value = true
    error.value = null

    try {
      await authService.signOut()
    } catch (err) {
      console.warn('Sign out API call failed:', err.message)
    } finally {
      user.value = null
      isAuthenticated.value = false
      tokenManager.clearTokens()
      isLoading.value = false
    }
  }

  const refreshUserProfile = async () => {
    if (!isAuthenticated.value) return

    try {
      const profileUser = await authService.getUserProfile()
      user.value = profileUser
    } catch (err) {
      error.value = err.message
      throw err
    }
  }

  const clearError = () => {
    error.value = null
  }

  const handleTokenExpired = () => {
    user.value = null
    isAuthenticated.value = false
    error.value = 'Your session has expired. Please sign in again.'
  }

  onMounted(() => {
    window.addEventListener('auth:token-expired', handleTokenExpired)
  })

  onUnmounted(() => {
    window.removeEventListener('auth:token-expired', handleTokenExpired)
  })

  return {
    isAuthenticated: computed(() => isAuthenticated.value),
    user: computed(() => user.value),
    isLoading: computed(() => isLoading.value),
    error: computed(() => error.value),
    initialize,
    signIn,
    signOut,
    refreshUserProfile,
    clearError
  }
}