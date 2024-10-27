using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Newtonsoft.Json;
using Al_Khayr_Salat.Functions;
using Avalonia.LogicalTree;
using Avalonia.Media;
using System.Media;
using static Al_Khayr_Salat.MainWindow;


namespace Al_Khayr_Salat;

public class PrayerTimesViewModel : BaseViewModel
{
    private DateTime[] _prayerTimes = new DateTime[5];
    private string[] _prayerTimesDisplay;
    public ObservableCollection<prayerTime> PrayerTimes { get; private set; }

    public PrayerTimesViewModel()
    {
       PrayerTimes = new ObservableCollection<prayerTime>
        {
            new prayerTime("Fajr", "00:00:00"),
            new prayerTime("Dhuhr", "00:00:00"),
            new prayerTime("Asr", "00:00:00"),
            new prayerTime("Maghrib", "00:00:00"),
            new prayerTime("Isha", "00:00:00")
        };
        // Trigger data fetching in a safe non-blocking way
        Task.Run(async () =>
        {
            await FetchPrayerTimes();
            PrayerTimeHasReached();
        });
    }

    private async Task FetchPrayerTimes()
    {
        try
        {
            string url = "https://mawaqit.net/nl/moskee-sounnat"; // Your URL

            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string htmlContent = await response.Content.ReadAsStringAsync();

                // Extract the JSON block that contains the prayer times
                var timesStartIndex = htmlContent.IndexOf("\"times\":") + 8;
                if (timesStartIndex < 8) throw new Exception("Unable to locate \"times\": in response");
                var timesEndIndex = htmlContent.IndexOf("]", timesStartIndex) + 1;
                if (timesEndIndex < timesStartIndex) throw new Exception("Unable to locate end of times array");
                var timesJson = htmlContent.Substring(timesStartIndex, timesEndIndex - timesStartIndex);
                Console.WriteLine(timesJson);
                // Convert the extracted times into a list of DateTime
                var times = JsonConvert.DeserializeObject<List<string>>(timesJson)
                    .Select(time => DateTime.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture))
                    .ToList();

                if (times.Count == 5)
                {
                    _prayerTimes = times.ToArray();
                    var prayerNames = new[] {"Fajr", "Dhuhr", "Asr", "Maghrib", "Isha"};
                    PrayerTimes.Clear();
                    
                    // Update UI with the fetched prayer times
                    for (int i = 0; i < prayerNames.Length; i++)
                    {
                        PrayerTimes.Add(new prayerTime(prayerNames[i], times[i].ToShortTimeString()));
                    }

                    // Start tracking the next prayer
                    StartNextPrayerCountdown();
                }
                else
                {
                    throw new Exception("Failed to extract all prayer times.");
                }
            }
        }
        catch (Exception ex)
        {
            // Handle any errors that occur during the request or parsing
            Console.WriteLine("Error fetching prayer times: " + ex.Message);
        }
    }
    private bool _adhanPlayed = false;
    private string _lastPrayerTime = string.Empty;
    
    private static readonly Mutex Mutex = new Mutex();
    private void PrayerTimeHasReached()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                DateTime currentTime = DateTime.Now;
                int adjustedIndex = nextPrayerIndex == 0 ? 4 : nextPrayerIndex - 1;
                Console.WriteLine(_prayerTimes[adjustedIndex].ToString("HH:mm") + " | " + currentTime.ToString("HH:mm"));
                if (_prayerTimes[adjustedIndex].ToString("HH:mm") == currentTime.ToString("HH:mm") || currentTime.ToString("HH:mm") == "15:25" && !_adhanPlayed)
                {
                    string currentprayerTime = _prayerTimes[adjustedIndex].ToString("HH:mm");
                    if (!_adhanPlayed && _lastPrayerTime != currentprayerTime)
                    {
                       /* var soundPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "adhan.wav");
                        var soundPlayer = new SoundPlayer(soundPath);

                        soundPlayer.LoadCompleted += (sender, args) =>
                        {
                            _adhanPlayed = false;
                            soundPlayer.Dispose();
                        };

                        Task.Run(() =>
                        {
                            soundPlayer.PlaySync(); // `PlaySync` waits for the sound to complete before continuing
                            _adhanPlayed = true;
                        }); */
                    }
                }

                if (nextPrayerTime.ToString("HH:mm") == "00:00" ||  _prayerTimes[4] == currentTime)
                {
                    Task.Run(async () => await FetchPrayerTimes());
                }

                await Task.Delay(1000);
            }
        });

    }

    public DateTime nextPrayerTime;
    public int nextPrayerIndex = 5;
    private void StartNextPrayerCountdown()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                DateTime currentTime = DateTime.Now;

                // Find the next prayer time that is greater than the current time
                nextPrayerTime = _prayerTimes.FirstOrDefault(time => time > currentTime);

                TimeSpan countdown;
                nextPrayerIndex = 5;
                if (nextPrayerTime != DateTime.MinValue)
                {
                    // Calculate the countdown to the next prayer time
                    countdown = nextPrayerTime - currentTime;
                    nextPrayerIndex = _prayerTimes
                        .Select((time, index) => new { time, index }) // Pair each time with its index
                        .FirstOrDefault(pair => pair.time.TimeOfDay == nextPrayerTime.TimeOfDay)?.index ?? -1;
                }
                else
                {
                    // If the current time is past the last prayer time for today,
                    // calculate for the first prayer time tomorrow
                    DateTime firstPrayerTimeTomorrow = _prayerTimes.First().AddDays(1);
                    countdown = firstPrayerTimeTomorrow - currentTime;
                    nextPrayerIndex = 0;
                }
                // Ensure the countdown is properly formatted
                string time = $"{countdown.Hours:D2}:{countdown.Minutes:D2}:{countdown.Seconds:D2}";
                
                try
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var updaterBlock = Instance.Find<TextBlock>("updater");
                        if (updaterBlock == null)
                        {
                            Console.WriteLine("TextBlock 'updater' not found.");
                        }
                        else
                        {
                            // Safely update the UI
                            updaterBlock.Text = time;
                        }
                        HighlightNextPrayerBorder(nextPrayerIndex);
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                // Wait 1 second before updating again
                await Task.Delay(1000);
            }
        });
    }
    private Border _currentHighlightedBorder;
    
    private void HighlightNextPrayerBorder(int prayerIndex)
    {
    // Reset previous border style if a border is currently highlighted
    if (_currentHighlightedBorder != null)
    {
        if (_currentHighlightedBorder is Border border)
        {
            // Change the background color of the highlighted border
            border.Background = new SolidColorBrush(Color.Parse("#191919"));

            // Reset TextBlock colors within the previous highlighted border
            if (border.Child is Panel panel)
            {
                // Iterate through the children of the panel and set the foreground color of TextBlock elements
                foreach (var textBlock in panel.Children.OfType<TextBlock>())
                {
                    textBlock.Foreground = new SolidColorBrush(Colors.White);
                }
            }
            else if (border.Child is Grid grid)
            {
                // Iterate through the logical children of the Grid and set the foreground color for TextBlocks
                foreach (var textBlock in grid.Children.OfType<TextBlock>())
                {
                    textBlock.Foreground = new SolidColorBrush(Colors.White);
                }
            }
        }

    }

    // Set the new highlighted border based on the next prayer time index
    _currentHighlightedBorder = prayerIndex switch
    {
        0 => Instance.Find<Border>("FajrBorder"),
        1 => Instance.Find<Border>("DhuhrBorder"),
        2 => Instance.Find<Border>("AsrBorder"),
        3 => Instance.Find<Border>("MaghribBorder"),
        4 => Instance.Find<Border>("IshaBorder"),
        _ => null
    };

    // Apply the highlight style if a valid border is found
    if (_currentHighlightedBorder != null)
    {
        // Set the LinearGradientBrush background to create the highlight effect
        _currentHighlightedBorder.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop { Color = Color.Parse("#f6b162"), Offset = 0 },
                new GradientStop { Color = Color.Parse("#f9f871"), Offset = 1 }
            }
        };

        // Update TextBlock colors within the highlighted border to black
        if (_currentHighlightedBorder is Border border)
        {
            // Check if the border's child is a layout container such as a Panel or Grid
            if (border.Child is Panel panel)
            {
                // Iterate through the children of the panel and set the foreground color of TextBlock elements
                foreach (var child in panel.Children.OfType<TextBlock>())
                {
                    child.Foreground = new SolidColorBrush(Colors.Black);
                }
            }
            else if (border.Child is Grid grid)
            {
                // Iterate through the logical children of the Grid and set the foreground color for TextBlocks
                foreach (var textBlock in grid.Children.OfType<TextBlock>())
                {
                    textBlock.Foreground = new SolidColorBrush(Colors.Black);
                }
            }
        }
    }
}
}