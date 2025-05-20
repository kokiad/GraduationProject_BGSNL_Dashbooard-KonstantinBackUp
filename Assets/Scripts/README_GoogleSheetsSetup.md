# Google Sheets Integration Setup Guide

This guide explains how to set up the Google Sheets API integration for the BGSNL Dashboard.

## 1. Google Cloud Setup

1. **Create a Google Cloud Project**:
   - Go to the [Google Cloud Console](https://console.cloud.google.com/)
   - Create a new project or select an existing one
   - Note your Project ID (bgsnl-dashboard)

2. **Enable the Google Sheets API**:
   - In the Google Cloud Console, navigate to "APIs & Services" > "Library"
   - Search for "Google Sheets API"
   - Click on it and press "Enable"

3. **Create API Credentials**:
   - Go to "APIs & Services" > "Credentials"
   - Click "Create Credentials" and select "API Key"
   - Copy the generated API key
   - Optionally, restrict the API key to only the Google Sheets API for security

## 2. Google Sheets Setup

1. **Create a Google Sheet**:
   - Create a new Google Sheet at [Google Sheets](https://sheets.google.com/)
   - Create two worksheets named exactly:
     - `SocialMedia`
     - `Events`

2. **Set up Headers**:
   - In the `SocialMedia` sheet, add the following headers in row 1:
     - `city_id` (required)
     - `instagram_followers`
     - `tiktok_followers`
     - `tiktok_likes`
     - `timestamp` (in format YYYY-MM-DD HH:MM:SS)

   - In the `Events` sheet, add the following headers in row 1:
     - `city_id` (required)
     - `tickets_sold`
     - `average_attendance`
     - `number_of_events`
     - `timestamp` (in format YYYY-MM-DD HH:MM:SS)

3. **Set Sharing Permissions**:
   - Click "Share" button in the top-right
   - Select "Anyone with the link" with Viewer permissions
   - Copy the Spreadsheet ID from the URL:
     - From `https://docs.google.com/spreadsheets/d/`**SPREADSHEET_ID**`/edit`
     - The ID is between `/d/` and `/edit`

## 3. Unity Setup

1. **Configure the GoogleSheetsService**:
   - In your Unity scene, add the `GoogleSheetsService` component to a GameObject
   - Reference your `DataModelClasses` component in the inspector
   - Enter the API Key and Spreadsheet ID in the inspector fields
   - Optionally, adjust the sheet names if you used different names
   - Adjust cache settings if needed

2. **Test the Integration**:
   - Enter some test data in your Google Sheets
   - Run the Unity project
   - Check the console for successful data retrieval
   - You should see messages about loading data and processing entries

## Troubleshooting

- **API Key Issues**: Ensure your API key has access to the Google Sheets API
- **Spreadsheet ID**: Double-check the spreadsheet ID is correct
- **Permission Denied**: Ensure your spreadsheet is shared with "Anyone with the link"
- **Missing Data**: Verify your column headers match exactly what the code expects
- **City ID**: Ensure the city IDs in your sheets match those in your Unity project 