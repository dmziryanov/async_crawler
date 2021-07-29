/*
The task is to implement a simple concurrent & asynchronous web crawler.

Since we don't want to make it too complex the Downloader and ReferenceExtractor
delegates are used to simulate the downloading of the page 
and parsing the page content (extracting URLs from the page content) respectively.

The CrawlerTester class is used for the testing purposes,
it will not give you any hints about the solution so you could just ignore this code.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Console;
using Guid = System.Guid;

namespace Challenges
{


    public class Page
    {
        public string Url { get; }
        public string Content { get; }

        public Page(string url, string content)
        {
            Url = url;
            Content = content;
        }

        public override string ToString() => $"Page, Url={Url}";
    }

    // This interface should be implemented
    public interface ICrawler
    {
        // A delegate downloading the page by its URL
        Func<string, CancellationToken, Task<Page>> Downloader { get; set; }

        // A delegate extracting a sequence of references (~hrefs) found on the page
        Func<Page, IEnumerable<string>> ReferenceExtractor { get; set; }

        // Crawls the whole graph of pages completely.
        // Should return IDictionary, where keys are URLs and values are corresponding downloaded pages.
        // Ideally, it should do this concurrently -- as efficiently as possible.
        Task<IDictionary<string, Page>> CrawlAsync(IEnumerable<string> urls, CancellationToken cancellationToken);
    }

    public class Crawler : ICrawler
    {
        // No need to implement anything - use it as-is
        public Func<string, CancellationToken, Task<Page>> Downloader { get; set; }

        // No need to implement anything - use it as-is
        public Func<Page, IEnumerable<string>> ReferenceExtractor { get; set; }

        [SuppressMessage("ReSharper.DPA", "DPA0002: Excessive memory allocations in SOH", MessageId = "type: System.String")]
        public async Task<IDictionary<string, Page>> CrawlAsync(IEnumerable<string> urls, CancellationToken cancellationToken)
        {
            var list = new ConcurrentDictionary<string, Page>();
            ConcurrentQueue<string> urlsQueue = new ConcurrentQueue<string>(urls.ToList());

            while (list.Count <= CrawlerTester.TotalPageCount)
            {
                if (urlsQueue.TryDequeue(out var url)) 

                await Task.Factory.StartNew(async () =>
                {
                    
                        list.TryAdd(url, await Downloader(url, cancellationToken));
                        ReferenceExtractor(list[url]).ToList().ForEach(x => urlsQueue.Enqueue(x));

                },  cancellationToken );


            }

            return list;
        }
    }



    public class CrawlerTester
    {
        public ICrawler Crawler { get; set; }
        public static int TotalPageCount { get; set; } = 5000;
        public TimeSpan MinPageDelayTime { get; set; } = TimeSpan.FromSeconds(0.1);
        public TimeSpan MaxPageDelayTime { get; set; } = TimeSpan.FromSeconds(1);
        public int MinPageReferenceCount { get; set; } = 0;
        public int MaxPageReferenceCount { get; set; } = 100;
        public Stopwatch Stopwatch { get; set; } = new Stopwatch();

        public static int _pagesDownloaded;
        private static long _elapsedMilliseconds;

        private static void DisplayStatus(object state)
        {
            if (_pagesDownloaded > 0)
                WriteLine($"Time elapsed: {_elapsedMilliseconds}ms, Pages downloaded: {_pagesDownloaded}");
        }

        // Emulates reference extraction
        public IEnumerable<string> ExtractReferences(Page page) => page.Content.Split();

        // Emulates page downloading
        public async Task<Page> Download(string url, CancellationToken cancellationToken)
        {
            Interlocked.Exchange(ref _elapsedMilliseconds, Stopwatch.ElapsedMilliseconds);

            // We want this method to behave the same every time it's called for the same URL
            var rnd = new Random(533000401 ^ url.GetHashCode());
            var delayDiff = MaxPageDelayTime - MinPageDelayTime;
            var delayTime = MinPageDelayTime +
                TimeSpan.FromSeconds(rnd.NextDouble() * delayDiff.TotalSeconds);
            var refCountDiff = MaxPageReferenceCount - MinPageReferenceCount;
            var refCount = MinPageReferenceCount + rnd.Next(refCountDiff);
            var content = string.Join(" ", Enumerable.Range(0, refCount).Select(_ => rnd.Next(TotalPageCount)));
            var page = new Page(url, content);
            await Task.Delay(delayTime, cancellationToken);

            Interlocked.Add(ref _pagesDownloaded, 1);
            Interlocked.Exchange(ref _elapsedMilliseconds, Stopwatch.ElapsedMilliseconds);

            return page;
        }

        public void Test(params string[] startUrls)
        {
            Crawler.Downloader = Download;
            Crawler.ReferenceExtractor = ExtractReferences;
            WriteLine($"Start crawling, max. pages: {TotalPageCount}");

            IDictionary<string, Page> pages;
            _elapsedMilliseconds = 0;
            _pagesDownloaded = 0;

            Stopwatch.Start();
            using (var timer = new Timer(DisplayStatus, null, 250, 250))
            {
                pages = Task.Run(() => Crawler.CrawlAsync(startUrls, CancellationToken.None)).Result;
            }
            Stopwatch.Stop();

            var allRefs = new HashSet<string>(startUrls.Union(pages.Values.SelectMany(ExtractReferences)));
            WriteLine();
            WriteLine($"Time taken, seconds: {Stopwatch.Elapsed.TotalSeconds}");
            WriteLine($"Pages downloaded:    {pages.Count}");
            WriteLine($"All refs crawled?    {allRefs.Count == pages.Count}");
            if (allRefs.Count != pages.Count)
                throw new InvalidOperationException("Some refs aren't crawled.");
        }
    }

    public class Program
    {
        public static void Main()
        {
            var tester = new CrawlerTester();
            var crawler = new Crawler()
            {
                Downloader = tester.Download,
                ReferenceExtractor = tester.ExtractReferences
            };
            tester.Crawler = crawler;
            tester.Test("0");

        }
    }
}
