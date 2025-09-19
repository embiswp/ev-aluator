<template>
  <div class="auth-guard">
    <!-- Show content if user is authenticated -->
    <slot v-if="isAuthenticated" name="authenticated" :user="user" />
    
    <!-- Show sign-in prompt if user is not authenticated -->
    <div v-else class="auth-required">
      <slot name="unauthenticated">
        <div class="signin-prompt">
          <h3>Sign In Required</h3>
          <p>Please sign in to access this feature.</p>
          <GoogleSignInButton 
            @sign-in-success="handleSignInSuccess"
            @sign-in-error="handleSignInError"
          />
        </div>
      </slot>
    </div>

    <!-- Show loading state -->
    <div v-if="isLoading" class="auth-loading">
      <div class="spinner"></div>
      <p>Checking authentication...</p>
    </div>
  </div>
</template>

<script>
import { useAuth } from '../../composables/useAuth.js'
import GoogleSignInButton from './GoogleSignInButton.vue'

export default {
  name: 'AuthGuard',
  components: {
    GoogleSignInButton
  },
  props: {
    requireAuth: {
      type: Boolean,
      default: true
    }
  },
  setup() {
    const { isAuthenticated, user, isLoading, initialize } = useAuth()
    
    initialize()
    
    return {
      isAuthenticated,
      user,
      isLoading
    }
  },
  methods: {
    handleSignInSuccess(result) {
      this.$emit('sign-in-success', result)
    },
    
    handleSignInError(error) {
      this.$emit('sign-in-error', error)
    }
  }
}
</script>

<style scoped>
.auth-guard {
  min-height: 200px;
}

.auth-required {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 200px;
}

.signin-prompt {
  text-align: center;
  padding: 2rem;
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #e9ecef;
  max-width: 400px;
  margin: 0 auto;
}

.signin-prompt h3 {
  margin: 0 0 1rem 0;
  color: #2c3e50;
}

.signin-prompt p {
  margin: 0 0 1.5rem 0;
  color: #6c757d;
}

.auth-loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 200px;
  gap: 1rem;
}

.spinner {
  width: 32px;
  height: 32px;
  border: 3px solid #f3f3f3;
  border-top: 3px solid #3498db;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.auth-loading p {
  color: #6c757d;
  margin: 0;
}
</style>