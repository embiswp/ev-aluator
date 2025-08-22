<template>
  <div class="evaluation-results">
    <h2>Evaluation Results</h2>
    
    <div v-if="!results" class="no-results">
      <p>No evaluation results available. Please upload location history and configure a vehicle first.</p>
    </div>
    
    <div v-else class="results-grid">
      <div class="result-card">
        <h3>Overall Feasibility</h3>
        <div class="feasibility-score" :class="feasibilityClass">
          {{ results.feasibilityScore }}%
        </div>
        <p>{{ feasibilityDescription }}</p>
      </div>
      
      <div class="result-card">
        <h3>Trip Analysis</h3>
        <div class="stat">
          <strong>Total Trips:</strong> {{ results.totalTrips }}
        </div>
        <div class="stat">
          <strong>Feasible Trips:</strong> {{ results.feasibleTrips }}
        </div>
        <div class="stat">
          <strong>Average Trip Distance:</strong> {{ results.averageTripDistance }} miles
        </div>
      </div>
      
      <div class="result-card">
        <h3>Range Requirements</h3>
        <div class="stat">
          <strong>Max Trip Distance:</strong> {{ results.maxTripDistance }} miles
        </div>
        <div class="stat">
          <strong>Recommended Range:</strong> {{ results.recommendedRange }} miles
        </div>
        <div class="stat">
          <strong>Charging Frequency:</strong> {{ results.chargingFrequency }}
        </div>
      </div>
      
      <div class="result-card">
        <h3>Problem Trips</h3>
        <div v-if="results.problematicTrips.length === 0">
          <p class="success">All trips can be completed with this vehicle! ✓</p>
        </div>
        <div v-else>
          <ul class="problem-list">
            <li v-for="trip in results.problematicTrips" :key="trip.id">
              {{ trip.distance }} miles - {{ trip.reason }}
            </li>
          </ul>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'EvaluationResults',
  props: {
    results: {
      type: Object,
      default: null
    }
  },
  computed: {
    feasibilityClass() {
      if (!this.results) return ''
      const score = this.results.feasibilityScore
      if (score >= 90) return 'excellent'
      if (score >= 70) return 'good'
      if (score >= 50) return 'moderate'
      return 'poor'
    },
    feasibilityDescription() {
      if (!this.results) return ''
      const score = this.results.feasibilityScore
      if (score >= 90) return 'Excellent! This EV is perfect for your travel patterns.'
      if (score >= 70) return 'Good fit! Minor adjustments may be needed for some trips.'
      if (score >= 50) return 'Moderate fit. Consider your charging options carefully.'
      return 'Poor fit. This EV may not meet your current travel needs.'
    }
  }
}
</script>

<style scoped>
.evaluation-results {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.no-results {
  text-align: center;
  color: #7f8c8d;
  font-style: italic;
}

.results-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 1.5rem;
  margin-top: 1rem;
}

.result-card {
  background: #f8f9fa;
  padding: 1.5rem;
  border-radius: 6px;
  border-left: 4px solid #3498db;
}

.result-card h3 {
  margin: 0 0 1rem 0;
  color: #2c3e50;
}

.feasibility-score {
  font-size: 3rem;
  font-weight: bold;
  text-align: center;
  margin: 1rem 0;
}

.feasibility-score.excellent { color: #27ae60; }
.feasibility-score.good { color: #f39c12; }
.feasibility-score.moderate { color: #e67e22; }
.feasibility-score.poor { color: #e74c3c; }

.stat {
  margin: 0.5rem 0;
  padding: 0.25rem 0;
  border-bottom: 1px solid #eee;
}

.stat:last-child {
  border-bottom: none;
}

.problem-list {
  list-style: none;
  padding: 0;
}

.problem-list li {
  background: #fff3cd;
  padding: 0.5rem;
  margin: 0.25rem 0;
  border-radius: 4px;
  border-left: 3px solid #ffc107;
}

.success {
  color: #27ae60;
  font-weight: 600;
  text-align: center;
}
</style>