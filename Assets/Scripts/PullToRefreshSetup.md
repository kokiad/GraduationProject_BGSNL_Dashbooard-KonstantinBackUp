# Pull-to-Refresh Setup Guide

This guide explains how to set up the pull-to-refresh functionality in your BGSNL Dashboard app.

## 1. Add the PullToRefresh Component

1. **Select your main UI container**:
   - In your Home scene, select the main panel or canvas that contains your dashboard content
   - This should be a UI element that can be pulled up (usually your main content panel)

2. **Add the PullToRefresh component**:
   - With the UI element selected, click "Add Component" in the Inspector
   - Type "Pull To Refresh" and select the script

## 2. Configure the Component

In the Inspector, you'll see the PullToRefresh component with the following settings:

1. **Refresh Settings**:
   - **Pull Threshold**: How far the user needs to pull up to trigger a refresh (default: 100f)
   - **Max Pull Distance**: Maximum distance the UI can be pulled (default: 150f) - lower this value to limit how far the UI can be pulled

2. **Animation Settings**:
   - **Snap Back Duration**: How long the snap-back animation takes (default: 0.5f)
   - **Snap Back Curve**: Animation curve that controls the timing of the snap-back
   - **Overshoot Bounces**: Number of elastic bounces when snapping back (default: 2)
   - **Overshoot Amount**: How pronounced the bouncing effect is (0-1, default: 0.2)

3. **Visual Settings**:
   - **Refresh Indicator**: Optional UI element that shows during refresh
   - **Debug Mode**: Enable to see detailed logs in the console (highly recommended for troubleshooting)

4. **References**:
   - **Content Rect Transform**: The UI element that will be pulled up
     - This can be left empty if you've added the component directly to the panel you want to pull
     - If you want a different panel to move, drag that panel here

## 3. Setting Up the Refresh Indicator

For the best experience, add a refresh indicator:

1. **Create a UI Image**:
   - Add a small Image GameObject as a child of your canvas
   - Position it near the bottom of your screen
   - Assign a circular or spinning icon texture

2. **Configure the Indicator**:
   - Make sure the image is in a fixed position that won't move with the panel
   - Drag this GameObject to the "Refresh Indicator" field in the PullToRefresh component

## 4. How the Fixed Refresh Process Works

The refreshing mechanism has been completely rewritten to solve the data disappearing issue:

1. **Direct Data Fetch Approach**:
   - The script now uses direct method calls to fetch data instead of using ForceRefresh
   - It directly calls FetchSocialMediaData() and FetchEventData() in sequence
   - This approach prevents data from being cleared during refresh
   
2. **Controlled Flow**:
   - First ensures the correct city is loaded
   - Then fetches social media data (waiting for completion)
   - Then fetches event data (waiting for completion)
   - Finally refreshes the UI to display the new data
   
3. **Data Preservation**:
   - This approach never clears existing data
   - Data stays visible in the inspector throughout the refresh process
   - New data is added/updated without removing the old data first

This direct fetch approach solves the issue where data would disappear from the Inspector during refresh.

## 5. Testing the Pull-to-Refresh

1. **Play Mode Testing**:
   - In the Unity Editor, enter Play mode
   - Click and drag upward from the bottom of your UI
   - Release after pulling past the threshold
   - The UI should snap back with an elastic animation and refresh your current city's data
   - Open the Inspector to verify data remains visible throughout the process

2. **Mobile Testing**:
   - Build and deploy to a mobile device
   - Swipe up from the bottom of the screen
   - When you've pulled far enough and release, it will refresh the data

## 6. Troubleshooting Data Issues

If you're experiencing issues with data not refreshing correctly:

1. **Enable Debug Mode**:
   - Turn on Debug Mode in the Inspector
   - Check the console for detailed logging of the data state at each step
   - Look for messages showing "Data state after direct fetches" to verify data is loading

2. **Manual Testing**:
   - Make a specific change to the spreadsheet (e.g., change Instagram followers number)
   - Perform a pull-to-refresh while watching the console logs
   - You should see the new data loaded without any disappearing data in the inspector

3. **Last Resort Option**:
   - If refresh still doesn't work, try calling the alternative method directly:
     ```csharp
     var pullToRefresh = FindObjectOfType<PullToRefresh>();
     if (pullToRefresh != null)
         pullToRefresh.FetchFreshDataWithoutClearing();
     ```

## 7. Adjusting the Feel

To create the perfect feel for your app:

1. **For Smoother Snap-Back**:
   - Increase **Snap Back Duration** for slower, more pronounced animation
   - Adjust **Snap Back Curve** to control the timing

2. **For More/Less Bounce**:
   - Increase **Overshoot Bounces** for more oscillations
   - Increase **Overshoot Amount** for more pronounced bouncing (0.1-0.3 feels natural)

3. **For Restricted Pull Distance**:
   - Decrease **Max Pull Distance** to limit how far users can pull (50-75 is often good)
   - Adjust **Pull Threshold** to control how much pull is needed to trigger refresh 