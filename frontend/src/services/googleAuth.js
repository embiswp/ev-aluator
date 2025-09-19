class GoogleAuthService {
  constructor() {
    this.isInitialized = false
    this.googleAuth = null
    this.clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID
  }

  async initialize() {
    if (this.isInitialized) return

    try {
      if (!this.clientId) {
        throw new Error('Google Client ID not configured')
      }

      await this.loadGoogleAPI()
      
      await new Promise((resolve, reject) => {
        window.gapi.load('auth2', {
          callback: resolve,
          onerror: reject
        })
      })

      this.googleAuth = await window.gapi.auth2.init({
        client_id: this.clientId
      })

      this.isInitialized = true
    } catch (error) {
      throw new Error(`Failed to initialize Google Auth: ${error.message}`)
    }
  }

  async loadGoogleAPI() {
    if (window.gapi) return

    return new Promise((resolve, reject) => {
      const script = document.createElement('script')
      script.src = 'https://apis.google.com/js/api.js'
      script.onload = resolve
      script.onerror = reject
      document.head.appendChild(script)
    })
  }

  async signIn() {
    await this.initialize()

    try {
      const googleUser = await this.googleAuth.signIn({
        scope: 'profile email'
      })

      const authResponse = googleUser.getAuthResponse()
      const profile = googleUser.getBasicProfile()

      return {
        idToken: authResponse.id_token,
        accessToken: authResponse.access_token,
        profile: {
          id: profile.getId(),
          name: profile.getName(),
          email: profile.getEmail(),
          picture: profile.getImageUrl()
        }
      }
    } catch (error) {
      if (error.error === 'popup_closed_by_user') {
        throw new Error('Sign-in was cancelled')
      }
      throw new Error(`Google sign-in failed: ${error.error || error.message}`)
    }
  }

  async signOut() {
    if (!this.isInitialized) return

    try {
      await this.googleAuth.signOut()
    } catch (error) {
      console.warn('Google sign-out failed:', error)
    }
  }

  async getCurrentUser() {
    await this.initialize()
    
    const googleUser = this.googleAuth.currentUser.get()
    if (!googleUser.isSignedIn()) {
      return null
    }

    const profile = googleUser.getBasicProfile()
    return {
      profile: {
        id: profile.getId(),
        name: profile.getName(),
        email: profile.getEmail(),
        picture: profile.getImageUrl()
      }
    }
  }

  isSignedIn() {
    return this.isInitialized && 
           this.googleAuth && 
           this.googleAuth.isSignedIn.get()
  }
}

export const googleAuthService = new GoogleAuthService()