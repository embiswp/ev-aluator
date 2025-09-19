import Cookies from 'js-cookie'

class TokenManager {
  constructor() {
    this.refreshTimer = null
  }

  getAccessToken() {
    return Cookies.get('access_token')
  }

  getRefreshToken() {
    return Cookies.get('refresh_token')
  }

  hasValidToken() {
    const token = this.getAccessToken()
    if (!token) return false

    try {
      const payload = JSON.parse(atob(token.split('.')[1]))
      const currentTime = Math.floor(Date.now() / 1000)
      return payload.exp > currentTime
    } catch {
      return false
    }
  }

  setTokens(accessToken, refreshToken, expiresAt) {
    const expirationDate = new Date(expiresAt)
    
    Cookies.set('access_token', accessToken, {
      expires: expirationDate,
      secure: true,
      sameSite: 'strict'
    })

    const refreshExpirationDate = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000)
    Cookies.set('refresh_token', refreshToken, {
      expires: refreshExpirationDate,
      secure: true,
      sameSite: 'strict'
    })

    this.scheduleTokenRefresh(expirationDate)
  }

  clearTokens() {
    Cookies.remove('access_token')
    Cookies.remove('refresh_token')
    this.clearRefreshTimer()
  }

  scheduleTokenRefresh(expirationDate) {
    this.clearRefreshTimer()

    const refreshTime = expirationDate.getTime() - Date.now() - 5 * 60 * 1000
    
    if (refreshTime > 0) {
      this.refreshTimer = setTimeout(async () => {
        try {
          const { authService } = await import('./authService.js')
          await authService.refreshToken()
        } catch (error) {
          console.error('Token refresh failed:', error)
          this.clearTokens()
          window.dispatchEvent(new CustomEvent('auth:token-expired'))
        }
      }, refreshTime)
    }
  }

  clearRefreshTimer() {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer)
      this.refreshTimer = null
    }
  }

  getUserFromToken() {
    const token = this.getAccessToken()
    if (!token) return null

    try {
      const payload = JSON.parse(atob(token.split('.')[1]))
      return {
        id: payload.nameid || payload.sub,
        email: payload.email,
        name: payload.name || payload.unique_name,
        googleId: payload.google_id
      }
    } catch {
      return null
    }
  }

  initialize() {
    const token = this.getAccessToken()
    if (token && this.hasValidToken()) {
      try {
        const payload = JSON.parse(atob(token.split('.')[1]))
        const expirationDate = new Date(payload.exp * 1000)
        this.scheduleTokenRefresh(expirationDate)
      } catch (error) {
        console.warn('Failed to parse token for refresh scheduling:', error)
      }
    }
  }
}

export const tokenManager = new TokenManager()