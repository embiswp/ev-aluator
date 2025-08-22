<template>
  <div class="location-upload">
    <h2>Upload Location History</h2>
    <div class="upload-area">
      <input 
        type="file" 
        ref="fileInput"
        @change="handleFileSelect"
        accept=".json,.csv,.kml"
        multiple
      />
      <p>Select your location history files (JSON, CSV, or KML format)</p>
    </div>
    
    <div v-if="uploadedFiles.length > 0" class="uploaded-files">
      <h3>Uploaded Files:</h3>
      <ul>
        <li v-for="file in uploadedFiles" :key="file.name">
          {{ file.name }} ({{ formatFileSize(file.size) }})
        </li>
      </ul>
    </div>
  </div>
</template>

<script>
export default {
  name: 'LocationUpload',
  data() {
    return {
      uploadedFiles: []
    }
  },
  methods: {
    handleFileSelect(event) {
      this.uploadedFiles = Array.from(event.target.files)
      this.$emit('files-selected', this.uploadedFiles)
    },
    formatFileSize(bytes) {
      if (bytes === 0) return '0 Bytes'
      const k = 1024
      const sizes = ['Bytes', 'KB', 'MB', 'GB']
      const i = Math.floor(Math.log(bytes) / Math.log(k))
      return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
    }
  }
}
</script>

<style scoped>
.location-upload {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  margin-bottom: 2rem;
}

.upload-area {
  border: 2px dashed #3498db;
  border-radius: 8px;
  padding: 2rem;
  text-align: center;
  background: #f8f9fa;
}

.upload-area:hover {
  background: #e9ecef;
}

.uploaded-files {
  margin-top: 1rem;
}

.uploaded-files ul {
  list-style: none;
  padding: 0;
}

.uploaded-files li {
  background: #e8f5e8;
  padding: 0.5rem;
  margin: 0.25rem 0;
  border-radius: 4px;
}
</style>