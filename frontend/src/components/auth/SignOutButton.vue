<template>
  <button 
    @click="handleSignOut" 
    :disabled="isSigningOut"
    class="signout-button"
  >
    <span v-if="!isSigningOut">Sign Out</span>
    <span v-else>Signing out...</span>
  </button>
</template>

<script>
import { googleAuthService } from '../../services/googleAuth.js'
import { authService } from '../../services/authService.js'
import { tokenManager } from '../../services/tokenManager.js'

export default {
  name: 'SignOutButton',
  data() {
    return {
      isSigningOut: false
    }
  },
  methods: {
    async handleSignOut() {
      this.isSigningOut = true

      try {
        await Promise.all([
          authService.signOut(),
          googleAuthService.signOut()
        ])

        tokenManager.clearTokens()
        
        this.$emit('sign-out-success')
        
      } catch (error) {
        console.error('Sign out error:', error)
        tokenManager.clearTokens()
        this.$emit('sign-out-success')
      } finally {
        this.isSigningOut = false
      }
    }
  }
}
</script>

<style scoped>
.signout-button {
  padding: 0.5rem 1rem;
  border: 1px solid #dc3545;
  border-radius: 4px;
  background: #dc3545;
  color: white;
  font-size: 0.875rem;
  cursor: pointer;
  transition: all 0.2s ease;
}

.signout-button:hover:not(:disabled) {
  background: #c82333;
  border-color: #bd2130;
}

.signout-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>