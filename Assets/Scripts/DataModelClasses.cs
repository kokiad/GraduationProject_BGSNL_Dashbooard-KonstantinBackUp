using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[Serializable]
public class City
{
    [SerializeField] private string name;
    [SerializeField] private string id;

    public string Name { get => name; set => name = value; }
    public string ID { get => id; set => id = value; }

    public City(string name, string id)
    {
        this.name = name;
        this.id = id;
    }
}

[Serializable]
public class SocialMediaMetrics
{
    [SerializeField] private string instagramFollowers;
    [SerializeField] private string tikTokFollowers;
    [SerializeField] private string tikTokLikes;
    [SerializeField] private City associatedCity;
    [SerializeField] private DateTime timestamp;

    public string InstagramFollowers { get => instagramFollowers; set => instagramFollowers = value; }
    public string TikTokFollowers { get => tikTokFollowers; set => tikTokFollowers = value; }
    public string TikTokLikes { get => tikTokLikes; set => tikTokLikes = value; }
    public City AssociatedCity { get => associatedCity; set => associatedCity = value; }
    public DateTime Timestamp { get => timestamp; set => timestamp = value; }

    public SocialMediaMetrics(string instagramFollowers, string tikTokFollowers, string tikTokLikes, City associatedCity, DateTime timestamp)
    {
        this.instagramFollowers = instagramFollowers;
        this.tikTokFollowers = tikTokFollowers;
        this.tikTokLikes = tikTokLikes;
        this.associatedCity = associatedCity;
        this.timestamp = timestamp;
    }

    public void UpdateFromRawData(Dictionary<string, string> rawData)
    {
        if (rawData.TryGetValue("instagram_followers", out string igFollowers))
        {
            instagramFollowers = igFollowers;
        }
        else
        {
            Debug.LogWarning("Raw data missing instagram_followers field");
            instagramFollowers = "0";
        }
        
        if (rawData.TryGetValue("tiktok_followers", out string ttFollowers))
        {
            tikTokFollowers = ttFollowers;
        }
        else
        {
            Debug.LogWarning("Raw data missing tiktok_followers field");
            tikTokFollowers = "0";
        }
        
        if (rawData.TryGetValue("tiktok_likes", out string ttLikes))
        {
            tikTokLikes = ttLikes;
        }
        else
        {
            Debug.LogWarning("Raw data missing tiktok_likes field");
            tikTokLikes = "0";
        }
        
        if (rawData.TryGetValue("timestamp", out string timestampStr))
        {
            if (DateTime.TryParse(timestampStr, out DateTime parsedTimestamp))
            {
                timestamp = parsedTimestamp;
            }
            else
            {
                timestamp = DateTime.Now;
                Debug.LogWarning($"Invalid timestamp format: {timestampStr}");
            }
        }
        else
        {
            timestamp = DateTime.Now;
        }
    }
}

[Serializable]
public class EventMetrics
{
    [SerializeField] private string ticketsSold;
    [SerializeField] private string averageAttendance;
    [SerializeField] private string numberOfEvents;
    [SerializeField] private City associatedCity;
    [SerializeField] private DateTime timestamp;

    public string TicketsSold { get => ticketsSold; set => ticketsSold = value; }
    public string AverageAttendance { get => averageAttendance; set => averageAttendance = value; }
    public string NumberOfEvents { get => numberOfEvents; set => numberOfEvents = value; }
    public City AssociatedCity { get => associatedCity; set => associatedCity = value; }
    public DateTime Timestamp { get => timestamp; set => timestamp = value; }

    public EventMetrics(string ticketsSold, string averageAttendance, string numberOfEvents, City associatedCity, DateTime timestamp)
    {
        this.ticketsSold = ticketsSold;
        this.averageAttendance = averageAttendance;
        this.numberOfEvents = numberOfEvents;
        this.associatedCity = associatedCity;
        this.timestamp = timestamp;
    }

    public void UpdateFromRawData(Dictionary<string, string> rawData)
    {
        Debug.Log($"Updating EventMetrics from raw data: {string.Join(", ", rawData.Select(kv => $"{kv.Key}={kv.Value}"))}");
        
        if (rawData.TryGetValue("tickets_sold", out string tickets))
        {
            ticketsSold = tickets;
            Debug.Log($"Set tickets_sold: {tickets}");
        }
        else
        {
            Debug.LogWarning("Raw data missing tickets_sold field");
            ticketsSold = "0";
        }
        
        if (rawData.TryGetValue("average_attendance", out string attendance))
        {
            averageAttendance = attendance;
            Debug.Log($"Set average_attendance: {attendance}");
        }
        else
        {
            Debug.LogWarning("Raw data missing average_attendance field");
            averageAttendance = "0";
        }
        
        if (rawData.TryGetValue("number_of_events", out string events))
        {
            numberOfEvents = events;
            Debug.Log($"Set number_of_events: {events}");
        }
        else
        {
            Debug.LogWarning("Raw data missing number_of_events field");
            numberOfEvents = "0";
        }
        
        if (rawData.TryGetValue("timestamp", out string timestampStr))
        {
            if (DateTime.TryParse(timestampStr, out DateTime parsedTimestamp))
            {
                timestamp = parsedTimestamp;
                Debug.Log($"Parsed timestamp as date: {timestampStr} -> {timestamp}");
            }
            else
            {
                timestamp = DateTime.Now;
                Debug.Log($"Timestamp value '{timestampStr}' is not a valid date. Using current time: {timestamp}");
            }
        }
        else
        {
            timestamp = DateTime.Now;
            Debug.Log("No timestamp field found. Using current time: " + timestamp);
        }
    }
}

public class DataModelClasses : MonoBehaviour
{
    [SerializeField] private List<City> cities = new List<City>();
    [SerializeField] private List<SocialMediaMetrics> socialMediaMetrics = new List<SocialMediaMetrics>();
    [SerializeField] private List<EventMetrics> eventMetrics = new List<EventMetrics>();

    public List<City> Cities => cities;
    public List<SocialMediaMetrics> SocialMediaMetrics => socialMediaMetrics;
    public List<EventMetrics> EventMetrics => eventMetrics;

    private void Start()
    {
        // Log the cities that were set up in Inspector
        if (cities.Count > 0)
        {
            Debug.Log($"DataModelClasses initialized with {cities.Count} cities:");
            foreach (var city in cities)
            {
                Debug.Log($"  - {city.Name} (ID: {city.ID})");
            }
        }
        else
        {
            Debug.Log("DataModelClasses initialized with no cities");
        }
    }

    // Retrieve a city by ID
    public City GetCityById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogError("GetCityById called with null or empty ID");
            return null;
        }
        
        City city = cities.Find(c => c.ID.ToLower() == id.ToLower());
        
        if (city == null)
        {
            Debug.LogWarning($"City with ID '{id}' not found. Available cities: {string.Join(", ", cities.Select(c => $"{c.Name} (ID: {c.ID})"))}");
        }
        
        return city;
    }

    // Add data methods
    public void AddCity(City city)
    {
        // Check if city with the same ID already exists
        if (cities.Any(c => c.ID.ToLower() == city.ID.ToLower()))
        {
            Debug.LogWarning($"City with ID '{city.ID}' already exists, not adding duplicate");
            return;
        }
        
        cities.Add(city);
        Debug.Log($"Added city: {city.Name} (ID: {city.ID}), total cities: {cities.Count}");
    }

    public void AddSocialMediaMetrics(SocialMediaMetrics metrics)
    {
        socialMediaMetrics.Add(metrics);
        Debug.Log($"Added social media metrics for {metrics.AssociatedCity.Name} (ID: {metrics.AssociatedCity.ID}): " +
                 $"Instagram={metrics.InstagramFollowers}, TikTok={metrics.TikTokFollowers}, Likes={metrics.TikTokLikes}");
    }

    public void AddEventMetrics(EventMetrics metrics)
    {
        eventMetrics.Add(metrics);
        Debug.Log($"Added event metrics for {metrics.AssociatedCity.Name} (ID: {metrics.AssociatedCity.ID}): " +
                 $"Tickets={metrics.TicketsSold}, Attendance={metrics.AverageAttendance}, Events={metrics.NumberOfEvents}");
    }
    
    // Clear data methods
    public void ClearSocialMediaMetrics()
    {
        int count = socialMediaMetrics.Count;
        socialMediaMetrics.Clear();
        Debug.Log($"Cleared {count} social media metrics entries");
    }
    
    public void ClearEventMetrics()
    {
        int count = eventMetrics.Count;
        eventMetrics.Clear();
        Debug.Log($"Cleared {count} event metrics entries");
    }

    // Get the latest metrics for a specific city
    public SocialMediaMetrics GetLatestSocialMediaMetrics(string cityId)
    {
        // Find all metrics for this city ID (case-insensitive)
        var metrics = socialMediaMetrics
            .FindAll(m => m.AssociatedCity.ID.ToLower() == cityId.ToLower());
        
        Debug.Log($"Found {metrics.Count} social media metrics entries for city ID '{cityId}'");
        
        if (metrics.Count > 0)
        {
            // Sort by timestamp (newest first)
            metrics = metrics.OrderByDescending(m => m.Timestamp).ToList();
            
            // Log all found entries to help debug
            for (int i = 0; i < metrics.Count; i++)
            {
                var metric = metrics[i];
                Debug.Log($"Social media metric {i+1} for {cityId}: " +
                          $"Instagram={metric.InstagramFollowers}, " +
                          $"TikTok={metric.TikTokFollowers}, " +
                          $"Likes={metric.TikTokLikes}, " +
                          $"Time={metric.Timestamp}");
            }
            
            // Return the one with non-zero values if possible
            foreach (var metric in metrics)
            {
                if (metric.InstagramFollowers != "0" || metric.TikTokFollowers != "0" || metric.TikTokLikes != "0")
                {
                    Debug.Log($"Using non-zero social media metric for {cityId}: " +
                             $"Instagram={metric.InstagramFollowers}, " +
                             $"TikTok={metric.TikTokFollowers}, " +
                             $"Likes={metric.TikTokLikes}");
                    return metric;
                }
            }
            
            // If all are zeroes, just return the newest one
            Debug.Log($"All social media metrics for {cityId} have zero values, using newest one");
            return metrics.First();
        }
        
        return null;
    }

    public EventMetrics GetLatestEventMetrics(string cityId)
    {
        if (string.IsNullOrEmpty(cityId))
        {
            Debug.LogWarning("[CRITICAL] GetLatestEventMetrics called with null or empty cityId");
            return null;
        }

        cityId = cityId.ToLower().Trim();
        Debug.Log($"[CRITICAL] Getting latest event metrics for city ID: {cityId}");

        var cityMetrics = eventMetrics
            .Where(m => m.AssociatedCity != null && m.AssociatedCity.ID.ToLower() == cityId)
            .ToList();
        
        Debug.Log($"[CRITICAL] Found {cityMetrics.Count} metrics for city {cityId}");
        
        if (cityMetrics.Count == 0)
        {
            Debug.LogWarning($"[CRITICAL] No metrics found for city {cityId}");
            return null;
        }

        // Sort by timestamp descending
        cityMetrics.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
            
        // Log all found metrics
        foreach (var metric in cityMetrics)
        {
            Debug.Log($"[CRITICAL] Found metric for {cityId}: " +
                          $"Tickets={metric.TicketsSold}, " +
                          $"Attendance={metric.AverageAttendance}, " +
                          $"Events={metric.NumberOfEvents}, " +
                     $"Timestamp={metric.Timestamp}");
            }
            
        // First try to find a complete metric (all non-zero values)
        var completeMetric = cityMetrics.FirstOrDefault(m => 
            m.TicketsSold != "0" && 
            m.AverageAttendance != "0" && 
            m.NumberOfEvents != "0");

        if (completeMetric != null)
                {
            Debug.Log($"[CRITICAL] Found complete metric for {cityId} with all non-zero values");
            return completeMetric;
        }

        // If no complete metric, look for a partial metric (at least one non-zero value)
        var partialMetric = cityMetrics.FirstOrDefault(m => 
            m.TicketsSold != "0" || 
            m.AverageAttendance != "0" || 
            m.NumberOfEvents != "0");

        if (partialMetric != null)
        {
            Debug.Log($"[CRITICAL] Found partial metric for {cityId} with some non-zero values");
            return partialMetric;
            }
            
        // If all metrics are zero, return the newest one
        Debug.Log($"[CRITICAL] All metrics for {cityId} are zero, returning newest");
        return cityMetrics[0];
    }
}
