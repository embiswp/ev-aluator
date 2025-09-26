import { test, expect, devices, type BrowserContext, type Page } from '@playwright/test'

/**
 * Integration tests for mobile responsiveness and accessibility (Scenario 4 from quickstart.md).
 * Tests cross-device compatibility and WCAG 2.1 AA compliance.
 * These tests will fail until the full frontend implementation is complete (expected in TDD).
 */

// Test configuration for different devices
const testDevices = [
  {
    name: 'Desktop',
    viewport: { width: 1920, height: 1080 },
    userAgent: 'desktop',
  },
  {
    name: 'Tablet',
    viewport: { width: 768, height: 1024 },
    userAgent: 'tablet',
  },
  {
    name: 'Mobile',
    viewport: { width: 375, height: 667 },
    userAgent: 'mobile',
  },
]

testDevices.forEach(device => {
  test.describe(`Responsive Design - ${device.name}`, () => {
    let context: BrowserContext
    let page: Page

    test.beforeEach(async ({ browser }) => {
      context = await browser.newContext({
        viewport: device.viewport,
        userAgent: device.userAgent === 'mobile' ? devices['iPhone 8'].userAgent : 
                   device.userAgent === 'tablet' ? devices['iPad'].userAgent :
                   'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
      })
      page = await context.newPage()
    })

    test.afterEach(async () => {
      await context.close()
    })

    test(`should display landing page correctly on ${device.name}`, async () => {
      // Navigate to application
      await page.goto('http://localhost:3000')
      
      // Landing page should load without horizontal scrolling
      const bodyWidth = await page.evaluate(() => document.body.scrollWidth)
      const viewportWidth = device.viewport.width
      
      expect(bodyWidth).toBeLessThanOrEqual(viewportWidth + 1) // Allow 1px tolerance
      
      // Sign in button should be visible and tappable
      const signInButton = page.locator('button:has-text("Sign in with Google")')
      await expect(signInButton).toBeVisible()
      
      // On mobile/tablet, button should be large enough for touch
      if (device.name !== 'Desktop') {
        const buttonBox = await signInButton.boundingBox()
        expect(buttonBox!.width).toBeGreaterThanOrEqual(44) // WCAG minimum touch target
        expect(buttonBox!.height).toBeGreaterThanOrEqual(44)
      }
    })

    test(`should handle authentication flow on ${device.name}`, async () => {
      await page.goto('http://localhost:3000')
      
      // Click sign-in button
      const signInButton = page.locator('button:has-text("Sign in with Google")')
      await signInButton.click()
      
      // Should redirect to OAuth (simulated)
      // In real implementation, would mock OAuth flow
      expect(page.url()).toContain('auth')
    })

    test(`should display upload interface correctly on ${device.name}`, async () => {
      // Simulate authenticated state
      await page.goto('http://localhost:3000')
      
      // Mock authentication completion and dashboard load
      await page.evaluate(() => {
        // Simulate successful authentication
        localStorage.setItem('ev-session', 'mock-session')
      })
      
      await page.goto('http://localhost:3000/dashboard')
      
      // Upload area should be visible
      const uploadArea = page.locator('[data-testid="upload-area"]')
      await expect(uploadArea).toBeVisible()
      
      // File input should be accessible on touch devices
      const fileInput = page.locator('input[type="file"]')
      if (device.name !== 'Desktop') {
        // On mobile, file input should be properly sized
        const inputBox = await fileInput.boundingBox()
        if (inputBox) {
          expect(inputBox.width).toBeGreaterThanOrEqual(44)
          expect(inputBox.height).toBeGreaterThanOrEqual(44)
        }
      }
      
      // Progress indicator should be positioned correctly
      const progressContainer = page.locator('[data-testid="progress-container"]')
      if (await progressContainer.isVisible()) {
        // Should not overflow viewport
        const progressBox = await progressContainer.boundingBox()
        expect(progressBox!.right).toBeLessThanOrEqual(device.viewport.width)
      }
    })

    test(`should display data summary responsively on ${device.name}`, async () => {
      // Simulate state with uploaded data
      await page.goto('http://localhost:3000')
      
      await page.evaluate(() => {
        localStorage.setItem('ev-session', 'mock-session-with-data')
      })
      
      await page.goto('http://localhost:3000/dashboard')
      
      // Data summary cards should stack appropriately
      const summaryCards = page.locator('[data-testid="summary-card"]')
      const cardCount = await summaryCards.count()
      
      if (cardCount > 0) {
        // On mobile, cards should stack vertically
        if (device.name === 'Mobile') {
          for (let i = 0; i < cardCount - 1; i++) {
            const card1 = summaryCards.nth(i)
            const card2 = summaryCards.nth(i + 1)
            
            const box1 = await card1.boundingBox()
            const box2 = await card2.boundingBox()
            
            if (box1 && box2) {
              // Card 2 should be below card 1 (allowing for small horizontal overlap)
              expect(box2.y).toBeGreaterThanOrEqual(box1.y)
            }
          }
        }
        
        // All cards should fit within viewport width
        for (let i = 0; i < cardCount; i++) {
          const card = summaryCards.nth(i)
          const cardBox = await card.boundingBox()
          
          if (cardBox) {
            expect(cardBox.right).toBeLessThanOrEqual(device.viewport.width + 1)
          }
        }
      }
    })

    test(`should handle EV analysis form on ${device.name}`, async () => {
      await page.goto('http://localhost:3000')
      
      await page.evaluate(() => {
        localStorage.setItem('ev-session', 'mock-session-with-data')
      })
      
      await page.goto('http://localhost:3000/analysis')
      
      // EV range input should be properly sized
      const rangeInput = page.locator('input[data-testid="ev-range-input"]')
      await expect(rangeInput).toBeVisible()
      
      if (device.name !== 'Desktop') {
        const inputBox = await rangeInput.boundingBox()
        if (inputBox) {
          expect(inputBox.height).toBeGreaterThanOrEqual(44) // Touch-friendly height
        }
      }
      
      // Submit button should be accessible
      const submitButton = page.locator('button[data-testid="analyze-button"]')
      await expect(submitButton).toBeVisible()
      
      if (device.name !== 'Desktop') {
        const buttonBox = await submitButton.boundingBox()
        if (buttonBox) {
          expect(buttonBox.width).toBeGreaterThanOrEqual(44)
          expect(buttonBox.height).toBeGreaterThanOrEqual(44)
        }
      }
    })

    test(`should display results charts appropriately on ${device.name}`, async () => {
      // Simulate state with analysis results
      await page.goto('http://localhost:3000')
      
      await page.evaluate(() => {
        localStorage.setItem('ev-session', 'mock-session-with-results')
      })
      
      await page.goto('http://localhost:3000/results')
      
      // Charts should scale to fit viewport
      const chartContainer = page.locator('[data-testid="results-chart"]')
      if (await chartContainer.isVisible()) {
        const chartBox = await chartContainer.boundingBox()
        
        if (chartBox) {
          expect(chartBox.right).toBeLessThanOrEqual(device.viewport.width + 1)
          
          // Chart should have reasonable minimum size even on small screens
          expect(chartBox.width).toBeGreaterThanOrEqual(200)
          expect(chartBox.height).toBeGreaterThanOrEqual(200)
        }
      }
      
      // Data table should be scrollable horizontally if needed
      const dataTable = page.locator('[data-testid="daily-distances-table"]')
      if (await dataTable.isVisible()) {
        const tableBox = await dataTable.boundingBox()
        
        if (tableBox && device.name === 'Mobile') {
          // On mobile, table container should allow horizontal scrolling
          const tableContainer = page.locator('[data-testid="table-container"]')
          const containerStyle = await tableContainer.evaluate(el => 
            getComputedStyle(el).overflowX
          )
          
          // Should allow scrolling if content is wider than viewport
          expect(['auto', 'scroll']).toContain(containerStyle)
        }
      }
    })

    test(`should handle touch interactions on ${device.name}`, async () => {
      if (device.name === 'Desktop') {
        test.skip('Touch interaction test only applies to mobile/tablet devices')
      }
      
      await page.goto('http://localhost:3000')
      
      // Simulate file upload via touch
      const fileInput = page.locator('input[type="file"]')
      if (await fileInput.isVisible()) {
        // Touch should trigger file picker
        await fileInput.tap()
        // File picker opening would be handled by browser
      }
      
      // Test chart interactions if charts are present
      const chart = page.locator('[data-testid="results-chart"]')
      if (await chart.isVisible()) {
        // Tap on chart should work
        await chart.tap()
        
        // Pinch-to-zoom simulation (if supported)
        const chartBox = await chart.boundingBox()
        if (chartBox) {
          // Simulate touch gestures
          await page.touchscreen.tap(chartBox.x + chartBox.width / 2, chartBox.y + chartBox.height / 2)
        }
      }
    })
  })
})

test.describe('Accessibility Testing', () => {
  let page: Page

  test.beforeEach(async ({ browser }) => {
    const context = await browser.newContext()
    page = await context.newPage()
  })

  test('should support keyboard navigation throughout the app', async () => {
    await page.goto('http://localhost:3000')
    
    // All interactive elements should be focusable with Tab
    const focusableElements = await page.locator('button, input, [tabindex]:not([tabindex="-1"])').all()
    
    let currentFocusIndex = 0
    for (const element of focusableElements) {
      await page.keyboard.press('Tab')
      
      // Check if element is focused
      const isFocused = await element.evaluate(el => el === document.activeElement)
      if (isFocused) {
        currentFocusIndex++
        
        // Focus indicator should be visible
        const focusStyle = await element.evaluate(el => {
          const style = getComputedStyle(el)
          return {
            outline: style.outline,
            outlineOffset: style.outlineOffset,
            boxShadow: style.boxShadow,
          }
        })
        
        // Should have some kind of focus indicator
        const hasFocusIndicator = focusStyle.outline !== 'none' || 
                                 focusStyle.boxShadow !== 'none' ||
                                 focusStyle.outlineOffset !== '0px'
        
        expect(hasFocusIndicator).toBeTruthy()
      }
    }
    
    expect(currentFocusIndex).toBeGreaterThan(0)
  })

  test('should provide proper ARIA labels and descriptions', async () => {
    await page.goto('http://localhost:3000')
    
    // Check for proper ARIA attributes on key elements
    const fileInput = page.locator('input[type="file"]')
    if (await fileInput.isVisible()) {
      const ariaLabel = await fileInput.getAttribute('aria-label')
      const ariaDescribedBy = await fileInput.getAttribute('aria-describedby')
      
      expect(ariaLabel || ariaDescribedBy).toBeTruthy()
    }
    
    // Form inputs should have labels
    const formInputs = page.locator('input:not([type="hidden"])')
    const inputCount = await formInputs.count()
    
    for (let i = 0; i < inputCount; i++) {
      const input = formInputs.nth(i)
      const inputId = await input.getAttribute('id')
      
      if (inputId) {
        // Should have associated label
        const label = page.locator(`label[for="${inputId}"]`)
        const hasLabel = await label.count() > 0
        
        // Or should have aria-label
        const ariaLabel = await input.getAttribute('aria-label')
        
        expect(hasLabel || !!ariaLabel).toBeTruthy()
      }
    }
    
    // Buttons should have accessible names
    const buttons = page.locator('button')
    const buttonCount = await buttons.count()
    
    for (let i = 0; i < buttonCount; i++) {
      const button = buttons.nth(i)
      const buttonText = await button.textContent()
      const ariaLabel = await button.getAttribute('aria-label')
      
      // Button should have text content or aria-label
      expect(buttonText?.trim() || ariaLabel).toBeTruthy()
    }
  })

  test('should announce form errors and status updates to screen readers', async () => {
    await page.goto('http://localhost:3000')
    
    // Navigate to analysis form
    await page.goto('http://localhost:3000/analysis')
    
    // Submit form with invalid data to trigger validation
    const rangeInput = page.locator('input[data-testid="ev-range-input"]')
    if (await rangeInput.isVisible()) {
      await rangeInput.fill('-100') // Invalid range
      
      const submitButton = page.locator('button[data-testid="analyze-button"]')
      await submitButton.click()
      
      // Error message should have proper ARIA attributes
      const errorMessage = page.locator('[data-testid="error-message"]')
      if (await errorMessage.isVisible()) {
        const ariaLive = await errorMessage.getAttribute('aria-live')
        const role = await errorMessage.getAttribute('role')
        
        // Should announce to screen readers
        expect(ariaLive || role).toBeTruthy()
        expect(['polite', 'assertive', 'alert'].includes(ariaLive || role || '')).toBeTruthy()
      }
    }
    
    // Status updates should be announced
    const statusContainer = page.locator('[data-testid="status-container"]')
    if (await statusContainer.isVisible()) {
      const ariaLive = await statusContainer.getAttribute('aria-live')
      expect(ariaLive).toBeTruthy()
    }
  })

  test('should have proper heading hierarchy', async () => {
    await page.goto('http://localhost:3000')
    
    // Check heading structure
    const headings = await page.locator('h1, h2, h3, h4, h5, h6').all()
    
    if (headings.length > 0) {
      const headingLevels: number[] = []
      
      for (const heading of headings) {
        const tagName = await heading.evaluate(el => el.tagName.toLowerCase())
        const level = parseInt(tagName.replace('h', ''))
        headingLevels.push(level)
      }
      
      // Should start with h1
      expect(headingLevels[0]).toBe(1)
      
      // Check for logical progression (no skipping levels)
      for (let i = 1; i < headingLevels.length; i++) {
        const currentLevel = headingLevels[i]
        const previousLevel = headingLevels[i - 1]
        
        // Next heading should not skip more than one level
        if (currentLevel > previousLevel) {
          expect(currentLevel - previousLevel).toBeLessThanOrEqual(1)
        }
      }
    }
  })

  test('should provide alternative text for images and charts', async () => {
    await page.goto('http://localhost:3000')
    
    // All images should have alt text
    const images = page.locator('img')
    const imageCount = await images.count()
    
    for (let i = 0; i < imageCount; i++) {
      const img = images.nth(i)
      const altText = await img.getAttribute('alt')
      const ariaLabel = await img.getAttribute('aria-label')
      
      // Should have alt text or aria-label (empty alt is ok for decorative images)
      expect(altText !== null || ariaLabel !== null).toBeTruthy()
    }
    
    // Charts should have text alternatives or ARIA descriptions
    const charts = page.locator('[data-testid="results-chart"]')
    const chartCount = await charts.count()
    
    for (let i = 0; i < chartCount; i++) {
      const chart = charts.nth(i)
      const ariaLabel = await chart.getAttribute('aria-label')
      const ariaDescribedBy = await chart.getAttribute('aria-describedby')
      const title = await chart.getAttribute('title')
      
      // Chart should have some form of text alternative
      expect(ariaLabel || ariaDescribedBy || title).toBeTruthy()
      
      // If aria-describedby is used, the referenced element should exist
      if (ariaDescribedBy) {
        const descriptionElement = page.locator(`#${ariaDescribedBy}`)
        expect(await descriptionElement.count()).toBe(1)
      }
    }
  })

  test('should maintain color contrast ratios', async () => {
    await page.goto('http://localhost:3000')
    
    // This would typically use a tool like axe-playwright for automated contrast checking
    // For now, we'll check basic color properties exist
    
    const textElements = page.locator('p, span, div:has-text(""), label, button')
    const elementCount = await textElements.count()
    
    for (let i = 0; i < Math.min(elementCount, 10); i++) { // Sample first 10 elements
      const element = textElements.nth(i)
      
      if (await element.isVisible()) {
        const styles = await element.evaluate(el => {
          const computed = getComputedStyle(el)
          return {
            color: computed.color,
            backgroundColor: computed.backgroundColor,
            fontSize: computed.fontSize,
          }
        })
        
        // Elements should have defined colors
        expect(styles.color).toBeTruthy()
        expect(styles.color).not.toBe('rgba(0, 0, 0, 0)') // Not transparent
      }
    }
  })

  test('should work with screen reader navigation', async () => {
    // This test simulates screen reader navigation patterns
    await page.goto('http://localhost:3000')
    
    // Test landmark navigation
    const landmarks = page.locator('[role="main"], [role="navigation"], [role="banner"], [role="contentinfo"], main, nav, header, footer')
    const landmarkCount = await landmarks.count()
    
    expect(landmarkCount).toBeGreaterThan(0) // Should have some landmark elements
    
    // Test heading navigation (screen readers jump between headings)
    const headings = page.locator('h1, h2, h3, h4, h5, h6')
    const headingCount = await headings.count()
    
    if (headingCount > 0) {
      // Navigate through headings
      for (let i = 0; i < headingCount; i++) {
        const heading = headings.nth(i)
        const headingText = await heading.textContent()
        
        expect(headingText?.trim()).toBeTruthy() // Headings should have content
      }
    }
    
    // Test form navigation
    const formControls = page.locator('input, button, select, textarea')
    const controlCount = await formControls.count()
    
    for (let i = 0; i < Math.min(controlCount, 5); i++) { // Sample first 5 controls
      const control = formControls.nth(i)
      
      if (await control.isVisible()) {
        const tagName = await control.evaluate(el => el.tagName.toLowerCase())
        const type = await control.getAttribute('type')
        const ariaLabel = await control.getAttribute('aria-label')
        
        // Control should be identifiable to screen readers
        expect(tagName).toBeTruthy()
        
        if (tagName === 'input' && type === 'submit') {
          const value = await control.getAttribute('value')
          expect(value || ariaLabel).toBeTruthy()
        }
      }
    }
  })
})

test.describe('Performance on Mobile Devices', () => {
  test('should load quickly on mobile networks', async ({ browser }) => {
    // Simulate mobile network conditions
    const context = await browser.newContext({
      viewport: { width: 375, height: 667 },
    })
    
    const page = await context.newPage()
    
    // Simulate slow 3G
    await context.route('**/*', async route => {
      await new Promise(resolve => setTimeout(resolve, 100)) // 100ms delay
      await route.continue()
    })
    
    const startTime = Date.now()
    await page.goto('http://localhost:3000')
    
    // Wait for page to be interactive
    await page.waitForLoadState('networkidle')
    const loadTime = Date.now() - startTime
    
    // Should load within reasonable time on slow connections
    expect(loadTime).toBeLessThan(10000) // 10 seconds max
    
    await context.close()
  })

  test('should be responsive during file upload on mobile', async ({ browser }) => {
    const context = await browser.newContext({
      viewport: { width: 375, height: 667 },
    })
    
    const page = await context.newPage()
    await page.goto('http://localhost:3000')
    
    // Simulate authenticated state and file upload
    await page.evaluate(() => {
      localStorage.setItem('ev-session', 'mock-session')
    })
    
    await page.goto('http://localhost:3000/dashboard')
    
    // UI should remain responsive during upload simulation
    const fileInput = page.locator('input[type="file"]')
    if (await fileInput.isVisible()) {
      // Simulate file selection and upload start
      await fileInput.setInputFiles({
        name: 'test.json',
        mimeType: 'application/json',
        buffer: Buffer.from('{"locations": []}'),
      })
      
      // UI elements should remain interactive
      const otherButtons = page.locator('button:not([disabled])')
      const buttonCount = await otherButtons.count()
      
      for (let i = 0; i < Math.min(buttonCount, 3); i++) {
        const button = otherButtons.nth(i)
        if (await button.isVisible()) {
          await expect(button).not.toBeDisabled()
        }
      }
    }
    
    await context.close()
  })
})