using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SkiaSharp;

BenchmarkRunner.Run<ImageResizeBenchmarks>(args: args);

[ShortRunJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "RatioSD")]
public class ImageResizeBenchmarks
{
    // Keep the underlying streams open for reuse in consecutive benchmarks
    private static readonly NonClosableMemoryStream SourceStream = new NonClosableMemoryStream();

    // Pre-allocate a large buffer to avoid resizing during the benchmark
    private static readonly NonClosableMemoryStream DestinationStream = new NonClosableMemoryStream(capacity: 4 * 1024 * 1024);

    // Ensure consistent output quality across all libraries
    private const int OutputQuality = 75;

    private static readonly DecoderOptions ImageSharpDecoderOptions = new DecoderOptions
    {
        TargetSize = new Size(OutputWidth, OutputHeight),
    };

    private static readonly JpegEncoder ImageSharpJpegEncoder = new JpegEncoder
    {
        Quality = OutputQuality,
    };

    private const int OutputWidth = 256;
    private const int OutputHeight = 256;

    private static readonly MagickGeometry MagickOutputSize = new MagickGeometry(OutputWidth, OutputHeight);

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        await using var stream = typeof(Program).Assembly.GetManifestResourceStream("Benchmarks.mona_lisa_square.jpg");

        if (stream == null)
        {
            throw new InvalidOperationException("Resource not found.");
        }

        await stream.CopyToAsync(SourceStream);

        // Disable libvips operations cache
        // https://www.libvips.org/API/current/How-it-works.html#:~:text=Operation%20cache
        NetVips.Cache.Max = 0;
    }

    [Benchmark(Baseline = true)]
    public async Task ImageSharpInfo()
    {
        ResetStreams();

        var image = await Image.IdentifyAsync(SourceStream);

        var width = image.Width;
        var height = image.Height;
        Console.WriteLine($"Image dimensions: {width}x{height}");
    }

    [Benchmark]
    public async Task MagickNetInfo()
    {
        ResetStreams();

        var image = new MagickImageInfo(SourceStream);

        var width = image.Width;
        var height = image.Height;
        Console.WriteLine($"Image dimensions: {width}x{height}");
    }

    [Benchmark]
    public void NetVipsInfo()
    {
        ResetStreams();

        using var image = NetVips.Image.NewFromStream(SourceStream, access: NetVips.Enums.Access.Sequential);
        var width = image.Width;
        var height = image.Height;
        Console.WriteLine($"Image dimensions: {width}x{height}");
    }

    [Benchmark]
    public void SkiaSharpInfo()
    {
        ResetStreams();

        using var skBitmap = SKBitmap.Decode(SourceStream);
        if (skBitmap != null)
        {
            var width = skBitmap.Width;
            var height = skBitmap.Height;
            Console.WriteLine($"Image dimensions: {width}x{height}");
        }
        else
        {
            throw new InvalidOperationException("Failed decoding");
        }
    }

    private static void ResetStreams()
    {
        SourceStream.Seek(0, SeekOrigin.Begin);
        DestinationStream.Seek(0, SeekOrigin.Begin);
    }

    private sealed class NonClosableMemoryStream : Stream
    {
        private readonly MemoryStream _underlyingStream;

        public NonClosableMemoryStream()
        {
            this._underlyingStream = new MemoryStream();
        }

        public NonClosableMemoryStream(int capacity)
        {
            this._underlyingStream = new MemoryStream(capacity);
        }

        public override bool CanRead => this._underlyingStream.CanRead;

        public override bool CanSeek => this._underlyingStream.CanSeek;

        public override bool CanWrite => this._underlyingStream.CanWrite;

        public override long Length => this._underlyingStream.Length;

        public override long Position
        {
            get => this._underlyingStream.Position;
            set => this._underlyingStream.Position = value;
        }

        public override void Flush()
        {
            this._underlyingStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this._underlyingStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this._underlyingStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this._underlyingStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this._underlyingStream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            // No-op
        }
    }
}