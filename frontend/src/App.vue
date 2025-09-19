<template>
  <div id="app">
    <header>
      <div class="header-content">
        <div class="logo-section">
          <h1>EvAluator</h1>
          <p>Electric Vehicle Feasibility Evaluation Tool</p>
        </div>
        
        <div class="auth-section">
          <UserProfile 
            v-if="isAuthenticated" 
            :user="user"
            :show-actions="false"
            @sign-out-success="handleSignOutSuccess"
          />
          <GoogleSignInButton 
            v-else
            @sign-in-success="handleSignInSuccess"
            @sign-in-error="handleSignInError"
          />
        </div>
      </div>
    </header>
    
    <main>
      <div v-if="authError" class="auth-error">
        <p>{{ authError }}</p>
        <button @click="clearAuthError">Dismiss</button>
      </div>
      
      <HomeView />
    </main>
  </div>
</template>

<script>
import { useAuth } from './composables/useAuth.js'
import GoogleSignInButton from './components/auth/GoogleSignInButton.vue'
import UserProfile from './components/auth/UserProfile.vue'
import HomeView from './views/HomeView.vue'

export default {
  name: 'App',
  components: {
    GoogleSignInButton,
    UserProfile,
    HomeView
  },
  setup() {
    const { isAuthenticated, user, error, initialize, clearError } = useAuth()
    
    initialize()
    
    return {
      isAuthenticated,
      user,
      authError: error,
      clearAuthError: clearError
    }
  },
  methods: {
    handleSignInSuccess(result) {
      console.log('Sign in successful:', result)
    },
    
    handleSignInError(error) {
      console.error('Sign in failed:', error)
    },
    
    handleSignOutSuccess() {
      console.log('Sign out successful')
    }
  }
}
</script>

<style>
#app {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  margin: 0;
  padding: 20px;
  min-height: 100vh;
  background-color: #f5f5f5;
}

header {
  margin-bottom: 2rem;
}

.header-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  max-width: 1200px;
  margin: 0 auto;
  gap: 2rem;
}

.logo-section {
  text-align: left;
}

.logo-section h1 {
  color: #2c3e50;
  margin: 0 0 0.5rem 0;
}

.logo-section p {
  color: #7f8c8d;
  font-size: 1.1em;
  margin: 0;
}

.auth-section {
  flex-shrink: 0;
}

main {
  max-width: 1200px;
  margin: 0 auto;
}

.auth-error {
  background: #f8d7da;
  color: #721c24;
  padding: 1rem;
  border-radius: 6px;
  margin-bottom: 2rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.auth-error p {
  margin: 0;
}

.auth-error button {
  background: transparent;
  border: 1px solid #721c24;
  color: #721c24;
  padding: 0.25rem 0.75rem;
  border-radius: 4px;
  cursor: pointer;
}

.auth-error button:hover {
  background: #721c24;
  color: white;
}

@media (max-width: 768px) {
  .header-content {
    flex-direction: column;
    text-align: center;
    gap: 1rem;
  }
  
  .logo-section {
    text-align: center;
  }
}
</style>