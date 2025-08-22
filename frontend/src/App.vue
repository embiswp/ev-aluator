<template>
  <div id="app">
    <header>
      <h1>EvAluator</h1>
      <p>Electric Vehicle Feasibility Evaluation Tool</p>
    </header>
    
    <main>
      <router-view v-if="$route" />
      <div v-else>
        <p>Welcome to EvAluator - analyze your location history to determine if an electric vehicle meets your needs.</p>
      </div>
    </main>
  </div>
</template>

<script>
export default {
  name: 'App'
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
  text-align: center;
  margin-bottom: 2rem;
}

header h1 {
  color: #2c3e50;
  margin-bottom: 0.5rem;
}

header p {
  color: #7f8c8d;
  font-size: 1.1em;
}

main {
  max-width: 1200px;
  margin: 0 auto;
}
</style>