# Achievement System Setup Guide - For Your Existing UI

This guide explains how to set up the achievement system with your existing UI structure.

## Your Current UI Structure

Based on your images, you have:
```
Achievements (parent)
├── InstagramFollowersAchievement
│   ├── Stat
│   │   └── Text (TMP) - shows "627/1000"
│   └── Progress
│       └── Text (TMP) - shows "Reach 1000 Instagram Followers"
```

The system is designed to work with this exact structure!

## Step 1: Scene Setup

### 1.1 Add Achievement System to Scene
1. Create an empty GameObject named "AchievementSystem"
2. Add the `AchievementSystem` script to it
3. Configure in inspector:
   - Check ✅ **"Use Custom Milestones"** to edit milestone values
   - Edit the milestone arrays (e.g., Instagram: 1000, 1500, 2500, 5000, 10000)
   - Right-click component → **"Apply Custom Milestones"** when done

### 1.2 Add Achievement UI Manager
1. Create an empty GameObject named "AchievementUIManager" 
2. Add the `AchievementUI` script to it
3. Configure in inspector:
   - **Achievements Parent**: Drag your "Achievements" GameObject here
   - **Auto Find Achievements**: ✅ Checked (it will find your existing UI automatically)

### 1.3 Add Integration Script
1. Add the `AchievementIntegration` script to the same GameObject
2. It will automatically find the other components

## Step 2: Your Achievement UI Objects

### 2.1 Expected Object Names
Create achievement objects under your "Achievements" parent with these names:
- `InstagramFollowersAchievement` ✅ (you already have this!)
- `TikTokFollowersAchievement`
- `TikTokLikesAchievement` 
- `TicketsSoldAchievement`
- `NumberOfEventsAchievement`
- `AverageAttendanceAchievement`

### 2.2 Structure for Each Achievement
Each achievement should have:
```
[AchievementType]Achievement
├── Stat
│   └── Text (TMP) - will show "627/1000"
└── Progress  
    └── Text (TMP) - will show "Reach 1000 Instagram Followers"
```

## Step 3: How It Works

### 3.1 Automatic Discovery
The system will:
1. Find your "Achievements" parent object
2. Look for achievement objects by name
3. Automatically find the Stat and Progress text components
4. Update them with real data from Google Sheets

### 3.2 Text Updates
- **Progress Text**: Shows the achievement title (e.g., "Reach 1000 Instagram Followers")
- **Stat Text**: Shows current progress (e.g., "627/1000")

### 3.3 Dynamic Milestones
When you reach 1000 followers, the text will automatically update to:
- Progress: "Reach 1500 Instagram Followers" (next milestone)
- Stat: "1000/1500"

## Step 4: Customization

### 4.1 Edit Milestones
In `AchievementSystem` inspector:
```csharp
Instagram Followers Milestones: [1000, 1500, 2500, 5000, 10000]
TikTok Followers Milestones: [500, 1000, 2500, 5000]
// etc.
```

### 4.2 Visual Styling
The system respects your existing visual design. It only updates:
- Text content
- Text color (green when completed)
- Show/hide achievements (hidden if no progress)

### 4.3 Trophy Collection (Optional)
If you want trophy rewards:
1. Create a trophy container in your UI
2. Assign it to "Trophy Container" in `AchievementUI`
3. Create a trophy prefab and assign it

## Step 5: Testing

### 5.1 Debug Menu Options
Right-click on `AchievementSystem` component:
- **"Debug Current Milestones"** - See what milestones are active
- **"Force Check Achievements"** - Manual progress check
- **"Clear All Achievement Data"** - Reset for testing

### 5.2 UI Debug Options
Right-click on `AchievementUI` component:
- **"Auto-Discover Achievements"** - Find UI elements
- **"Refresh Display"** - Update display manually

## Step 6: Integration with Your App

### 6.1 Works with Existing Systems
The achievement system integrates with:
- Your existing `GoogleSheetsService` (gets data automatically)
- Your existing `DataModelClasses` (uses current metrics)
- Your existing `UIManager` (updates when city changes)
- Your existing `PullToRefresh` (checks achievements after refresh)

### 6.2 City-Specific Achievements
Each city has its own achievements:
- BGSNL: Achievements for BGSNL's Instagram followers, events, etc.
- Groningen: Separate achievements for Groningen's metrics
- etc.

## Complete Example

With your current "InstagramFollowersAchievement" and 627 followers, targeting 1000:

**Before milestone:**
- Progress Text: "Reach 1000 Instagram Followers"
- Stat Text: "627/1000"

**After reaching 1000:**
- Progress Text: "Reach 1500 Instagram Followers" 
- Stat Text: "1000/1500"
- Achievement unlocked notification (if enabled)
- Trophy awarded (if trophy system enabled)

## Troubleshooting

### If achievements don't update:
1. Check that `AchievementSystem` has `DataModelClasses` reference
2. Verify your achievement object names match expected names
3. Use "Force Check Achievements" context menu
4. Check console for debug messages

### If UI doesn't show:
1. Verify "Achievements Parent" is assigned
2. Use "Auto-Discover Achievements" context menu  
3. Check that Stat/Progress objects have correct names
4. Verify Text (TMP) components exist

The system is designed to work seamlessly with your existing beautiful UI! 🎯

## ❓ **Troubleshooting Your Specific Issues:**

### **Issue 1: City Detection per Scene**
✅ **SOLVED!** The system now automatically detects which city's achievements to show based on your scene name:

- **Your scene**: `BGSA_achievements` 
- **Auto-detected city**: `bgsa` (Amsterdam)
- **Shows**: Amsterdam's Instagram followers, events, etc.

**How it works:**
1. When the scene loads, it reads the scene name
2. Extracts city code from "BGSA_achievements" → "bgsa"  
3. Automatically shows Amsterdam's achievement data
4. Each scene will show the correct city's achievements!

### **Issue 2: UI Manager Reference**
✅ **SOLVED!** The "UI Manager" field confusion is clarified:

**What the field expects:**
- **NOT** your Achievement UI object
- **YES** your main `UIManager` component (from dashboard scenes)

**For achievement scenes:**
- ✅ Leave "UI Manager" as "None" - it's optional!
- ✅ Only assign it if you have a UIManager component in this scene
- ✅ The system works perfectly without it

## 🎯 **Your Setup (Based on Your Images):**

### **BGSA_achievements Scene Setup:**
1. ✅ **Achievement System**: Assigned
2. ✅ **Achievement UI**: Assigned  
3. ✅ **UI Manager**: Leave as "None" (that's correct!)
4. ✅ **Auto Find Achievements**: Checked
5. ✅ **Achievements Parent**: Set to your "Achievements" object

### **Scene-Based City Detection:**
```
BGSA_achievements → Shows Amsterdam (bgsa) achievements
BGSG_achievements → Shows Groningen (bgsg) achievements  
BGSR_achievements → Shows Rotterdam (bgsr) achievements
etc.
```

## 🔧 **Test Your Setup:**

### **Context Menu Tests:**
Right-click on `AchievementIntegration`:
- **"Detect City From Scene"** → Should show "bgsa" 
- **"Test Achievement Integration"** → Should show all components found
- **"Force Refresh All"** → Should update with Amsterdam's real data

### **Expected Results in BGSA_achievements:**
- Shows Amsterdam's Instagram followers (if they have data)
- Shows Amsterdam's event metrics  
- Progress like "627/1000" updates with Amsterdam's real numbers
- NOT BGSNL's data or other cities' data

## 🎉 **What's Fixed:**

1. **Scene Detection**: Each achievements scene automatically shows the right city
2. **UI Manager**: Optional field, can be left empty in achievement scenes  
3. **Auto-Discovery**: Finds your existing UI structure automatically
4. **Real Data**: Shows actual Google Sheets data for each city

Your existing setup should now work perfectly! The system will automatically detect that you're in the BGSA_achievements scene and show Amsterdam's achievement data. 🚀 