<template>
  <div class="home-view">
    <div class="hero-section">
      <h1>EvAluator</h1>
      <p class="subtitle">Evaluate Electric Vehicle Feasibility Based on Your Travel History</p>
      <p class="description">
        Upload your location history and vehicle specifications to analyze whether 
        an electric vehicle meets your travel needs.
      </p>
    </div>

    <div class="workflow-steps">
      <div class="step" :class="{ active: currentStep === 1 }">
        <div class="step-number">1</div>
        <h3>Upload Location History</h3>
        <p>Import your GPS data from Google, Apple, or other location services</p>
      </div>
      
      <div class="step" :class="{ active: currentStep === 2 }">
        <div class="step-number">2</div>
        <h3>Configure Vehicle</h3>
        <p>Enter the electric vehicle specifications you're considering</p>
      </div>
      
      <div class="step" :class="{ active: currentStep === 3 }">
        <div class="step-number">3</div>
        <h3>Get Results</h3>
        <p>View detailed analysis of feasibility and recommendations</p>
      </div>
    </div>

    <AuthGuard>
      <template #authenticated="{ user }">
        <LocationUpload 
          @files-selected="handleFilesSelected"
          class="component-section"
        />
        
        <VehicleConfiguration 
          v-if="filesUploaded"
          @vehicle-configured="handleVehicleConfigured"
          class="component-section"
        />
        
        <EvaluationResults 
          v-if="evaluationComplete"
          :results="evaluationResults"
          class="component-section"
        />
      </template>
      
      <template #unauthenticated>
        <div class="auth-prompt">
          <h3>Welcome to EvAluator!</h3>
          <p>Sign in with your Google account to start analyzing your electric vehicle feasibility.</p>
          <div class="features-list">
            <div class="feature">
              <h4>📍 Location Analysis</h4>
              <p>Upload your location history from Google, Apple, or other services</p>
            </div>
            <div class="feature">
              <h4>🚗 Vehicle Comparison</h4>
              <p>Compare different electric vehicles against your travel patterns</p>
            </div>
            <div class="feature">
              <h4>📊 Detailed Reports</h4>
              <p>Get comprehensive feasibility reports with recommendations</p>
            </div>
          </div>
          <GoogleSignInButton 
            @sign-in-success="handleSignInSuccess"
            @sign-in-error="handleSignInError"
          />
        </div>
      </template>
    </AuthGuard>

    <div v-if="vehicleConfigured && !evaluationComplete" class="analyze-section">
      <button @click="performAnalysis" class="analyze-btn" :disabled="analyzing">
        {{ analyzing ? 'Analyzing...' : 'Analyze Feasibility' }}
      </button>
    </div>
  </div>
</template>

<script>
import LocationUpload from '../components/LocationUpload.vue'
import VehicleConfiguration from '../components/VehicleConfiguration.vue'
import EvaluationResults from '../components/EvaluationResults.vue'
import AuthGuard from '../components/auth/AuthGuard.vue'
import GoogleSignInButton from '../components/auth/GoogleSignInButton.vue'

export default {
  name: 'HomeView',
  components: {
    LocationUpload,
    VehicleConfiguration,
    EvaluationResults,
    AuthGuard,
    GoogleSignInButton
  },
  data() {
    return {
      currentStep: 1,
      filesUploaded: false,
      vehicleConfigured: false,
      evaluationComplete: false,
      analyzing: false,
      uploadedFiles: [],
      vehicleConfig: null,
      evaluationResults: null
    }
  },
  methods: {
    handleFilesSelected(files) {
      this.uploadedFiles = files
      this.filesUploaded = true
      this.currentStep = 2
    },
    handleVehicleConfigured(config) {
      this.vehicleConfig = config
      this.vehicleConfigured = true
      this.currentStep = 3
    },
    handleSignInSuccess(result) {
      console.log('User signed in:', result)
    },
    handleSignInError(error) {
      console.error('Sign in failed:', error)
    },
    async performAnalysis() {
      this.analyzing = true
      try {
        this.evaluationResults = {
          feasibilityScore: 85,
          totalTrips: 245,
          feasibleTrips: 208,
          averageTripDistance: 12.3,
          maxTripDistance: 185,
          recommendedRange: 200,
          chargingFrequency: '2-3 times per week',
          problematicTrips: [
            { id: 1, distance: 185, reason: 'Exceeds single charge range' },
            { id: 2, distance: 165, reason: 'Limited charging options on route' }
          ]
        }
        this.evaluationComplete = true
      } catch (error) {
        console.error('Analysis failed:', error)
      } finally {
        this.analyzing = false
      }
    }
  }
}
</script>

<style scoped>
.home-view {
  max-width: 1200px;
  margin: 0 auto;
}

.hero-section {
  text-align: center;
  margin-bottom: 3rem;
  padding: 2rem;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border-radius: 12px;
}

.hero-section h1 {
  font-size: 3rem;
  margin-bottom: 0.5rem;
}

.subtitle {
  font-size: 1.3rem;
  margin-bottom: 1rem;
  opacity: 0.9;
}

.description {
  font-size: 1.1rem;
  max-width: 600px;
  margin: 0 auto;
  opacity: 0.8;
}

.workflow-steps {
  display: flex;
  justify-content: space-around;
  margin-bottom: 3rem;
  padding: 0 1rem;
}

.step {
  text-align: center;
  flex: 1;
  padding: 1rem;
  transition: all 0.3s ease;
}

.step.active {
  transform: scale(1.05);
}

.step-number {
  width: 50px;
  height: 50px;
  border-radius: 50%;
  background: #ecf0f1;
  color: #7f8c8d;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0 auto 1rem;
  font-size: 1.5rem;
  font-weight: bold;
  transition: all 0.3s ease;
}

.step.active .step-number {
  background: #3498db;
  color: white;
}

.step h3 {
  color: #2c3e50;
  margin-bottom: 0.5rem;
}

.step p {
  color: #7f8c8d;
  font-size: 0.9rem;
}

.component-section {
  margin-bottom: 2rem;
}

.analyze-section {
  text-align: center;
  margin: 3rem 0;
}

.analyze-btn {
  background: #e74c3c;
  color: white;
  padding: 1rem 2rem;
  border: none;
  border-radius: 8px;
  font-size: 1.2rem;
  cursor: pointer;
  transition: background-color 0.2s;
}

.analyze-btn:hover:not(:disabled) {
  background: #c0392b;
}

.analyze-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.auth-prompt {
  text-align: center;
  padding: 3rem 2rem;
  background: white;
  border-radius: 12px;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
  max-width: 800px;
  margin: 0 auto;
}

.auth-prompt h3 {
  color: #2c3e50;
  margin-bottom: 1rem;
  font-size: 1.8rem;
}

.auth-prompt > p {
  color: #7f8c8d;
  font-size: 1.1rem;
  margin-bottom: 2rem;
  max-width: 600px;
  margin-left: auto;
  margin-right: auto;
}

.features-list {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 2rem;
  margin: 2rem 0 3rem 0;
}

.feature {
  padding: 1.5rem;
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #e9ecef;
}

.feature h4 {
  color: #2c3e50;
  margin: 0 0 0.5rem 0;
  font-size: 1.1rem;
}

.feature p {
  color: #6c757d;
  margin: 0;
  font-size: 0.9rem;
  line-height: 1.4;
}

@media (max-width: 768px) {
  .workflow-steps {
    flex-direction: column;
  }
  
  .hero-section h1 {
    font-size: 2rem;
  }
  
  .subtitle {
    font-size: 1.1rem;
  }
  
  .features-list {
    grid-template-columns: 1fr;
    gap: 1rem;
  }
  
  .auth-prompt {
    padding: 2rem 1rem;
  }
}
</style>