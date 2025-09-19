<template>
  <div class="user-profile">
    <div class="profile-info">
      <div class="profile-avatar">
        <img 
          v-if="user.pictureUrl" 
          :src="user.pictureUrl" 
          :alt="`${user.name}'s profile picture`"
        />
        <div v-else class="default-avatar">
          {{ user.name.charAt(0).toUpperCase() }}
        </div>
      </div>
      
      <div class="profile-details">
        <div class="profile-name">{{ user.name }}</div>
        <div class="profile-email">{{ user.email }}</div>
      </div>
    </div>

    <div v-if="showActions" class="profile-actions">
      <button @click="$emit('view-profile')" class="action-button secondary">
        View Profile
      </button>
      <SignOutButton @sign-out-success="$emit('sign-out-success')" />
    </div>
  </div>
</template>

<script>
import SignOutButton from './SignOutButton.vue'

export default {
  name: 'UserProfile',
  components: {
    SignOutButton
  },
  props: {
    user: {
      type: Object,
      required: true
    },
    showActions: {
      type: Boolean,
      default: true
    }
  },
  emits: ['view-profile', 'sign-out-success']
}
</script>

<style scoped>
.user-profile {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #e9ecef;
}

.profile-info {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex: 1;
}

.profile-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  overflow: hidden;
  background: #6c757d;
  display: flex;
  align-items: center;
  justify-content: center;
}

.profile-avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.default-avatar {
  color: white;
  font-weight: 600;
  font-size: 1rem;
}

.profile-details {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.profile-name {
  font-weight: 600;
  color: #2c3e50;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.profile-email {
  font-size: 0.875rem;
  color: #6c757d;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.profile-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.action-button {
  padding: 0.5rem 1rem;
  border: 1px solid #dee2e6;
  border-radius: 4px;
  background: #fff;
  color: #495057;
  font-size: 0.875rem;
  cursor: pointer;
  transition: all 0.2s ease;
}

.action-button:hover {
  background: #e9ecef;
  border-color: #adb5bd;
}

.action-button.secondary {
  background: #6c757d;
  color: white;
  border-color: #6c757d;
}

.action-button.secondary:hover {
  background: #5a6268;
  border-color: #545b62;
}

@media (max-width: 480px) {
  .user-profile {
    flex-direction: column;
    align-items: stretch;
    text-align: center;
  }
  
  .profile-info {
    flex-direction: column;
    text-align: center;
  }
  
  .profile-actions {
    justify-content: center;
  }
}
</style>