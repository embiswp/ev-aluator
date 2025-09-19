import { ref } from 'vue'
import { googleAuthService } from '../services/googleAuth.js'
import { useAuth } from './useAuth.js'

export function useGoogleAuth() {
  const { signIn } = useAuth()
  const isSigningIn = ref(false)
  const error = ref(null)

  const signInWithGoogle = async () => {
    isSigningIn.value = true
    error.value = null

    try {
      const googleResult = await googleAuthService.signIn()
      const authResult = await signIn(googleResult.idToken)
      
      return {
        success: true,
        user: authResult.user,
        expiresAt: authResult.expiresAt
      }
    } catch (err) {
      error.value = err.message
      throw err
    } finally {
      isSigningIn.value = false
    }
  }

  const getCurrentGoogleUser = async () => {
    try {
      return await googleAuthService.getCurrentUser()
    } catch (err) {
      console.warn('Failed to get current Google user:', err.message)
      return null
    }
  }

  const isGoogleSignedIn = () => {
    return googleAuthService.isSignedIn()
  }

  const signOutFromGoogle = async () => {
    try {
      await googleAuthService.signOut()
    } catch (err) {
      console.warn('Google sign out failed:', err.message)
    }
  }

  const clearError = () => {
    error.value = null
  }

  return {
    isSigningIn,
    error,
    signInWithGoogle,
    getCurrentGoogleUser,
    isGoogleSignedIn,
    signOutFromGoogle,
    clearError
  }
}