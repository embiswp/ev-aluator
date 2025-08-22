<template>
  <div class="vehicle-config">
    <h2>Vehicle Configuration</h2>
    <form @submit.prevent="handleSubmit">
      <div class="form-group">
        <label for="vehicleName">Vehicle Name:</label>
        <input 
          id="vehicleName"
          v-model="vehicle.name"
          type="text"
          placeholder="e.g., Tesla Model 3"
          required
        />
      </div>
      
      <div class="form-group">
        <label for="batteryRange">Battery Range (miles):</label>
        <input 
          id="batteryRange"
          v-model.number="vehicle.batteryRange"
          type="number"
          min="1"
          max="1000"
          placeholder="e.g., 310"
          required
        />
      </div>
      
      <div class="form-group">
        <label for="chargingSpeed">Charging Speed (miles/hour):</label>
        <input 
          id="chargingSpeed"
          v-model.number="vehicle.chargingSpeed"
          type="number"
          min="1"
          max="1000"
          placeholder="e.g., 170"
        />
      </div>
      
      <div class="form-group">
        <label for="efficiency">Efficiency (miles/kWh):</label>
        <input 
          id="efficiency"
          v-model.number="vehicle.efficiency"
          type="number"
          step="0.1"
          min="0.1"
          max="10"
          placeholder="e.g., 4.2"
        />
      </div>
      
      <button type="submit" class="submit-btn">Save Configuration</button>
    </form>
  </div>
</template>

<script>
export default {
  name: 'VehicleConfiguration',
  data() {
    return {
      vehicle: {
        name: '',
        batteryRange: null,
        chargingSpeed: null,
        efficiency: null
      }
    }
  },
  methods: {
    handleSubmit() {
      this.$emit('vehicle-configured', this.vehicle)
    }
  }
}
</script>

<style scoped>
.vehicle-config {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  margin-bottom: 2rem;
}

.form-group {
  margin-bottom: 1rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 600;
  color: #2c3e50;
}

.form-group input {
  width: 100%;
  padding: 0.75rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 1rem;
}

.form-group input:focus {
  outline: none;
  border-color: #3498db;
  box-shadow: 0 0 0 2px rgba(52, 152, 219, 0.2);
}

.submit-btn {
  background: #27ae60;
  color: white;
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 4px;
  font-size: 1rem;
  cursor: pointer;
  transition: background-color 0.2s;
}

.submit-btn:hover {
  background: #229954;
}
</style>