using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Shared
{
    public class RateLimitHandler
    {
        // Existing RateLimitHandler implementation remains the same
        private DateTime? _nextAllowedRequest;
        private readonly object _lockObject = new object();

        public async Task<T> ExecuteWithRateLimitHandling<T>(Func<Task<T>> operation)
        {
            while (true)
            {
                lock (_lockObject)
                {
                    if (_nextAllowedRequest == null || DateTime.Now >= _nextAllowedRequest)
                    {
                        _nextAllowedRequest = null;
                    }
                }

                if (_nextAllowedRequest == null)
                {
                    try
                    {
                        return await operation();
                    }
                    catch (Exception ex)
                    {
                        var match = Regex.Match(ex.Message, @"retry after (\d+) seconds");
                        if (match.Success)
                        {
                            int waitSeconds = int.Parse(match.Groups[1].Value);
                            lock (_lockObject)
                            {
                                _nextAllowedRequest = DateTime.Now.AddSeconds(waitSeconds);
                            }
                            Console.WriteLine($"Rate limited. Waiting {waitSeconds} seconds.");
                            await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
                        }
                        else
                        {
                            throw; // Re-throw if it's not a rate limit error
                        }
                    }
                }
                else
                {
                    var waitTime = _nextAllowedRequest.Value - DateTime.Now;
                    if (waitTime > TimeSpan.Zero)
                    {
                        Console.WriteLine($"Waiting {waitTime.TotalSeconds} seconds before next request.");
                        await Task.Delay(waitTime);
                    }
                }
            }
        }
    }
}
