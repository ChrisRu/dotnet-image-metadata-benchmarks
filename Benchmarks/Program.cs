using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;

BenchmarkRunner.Run<ImageResizeBenchmarks>(args: args);

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class ImageResizeBenchmarks
{
    // Keep the underlying streams open for reuse in consecutive benchmarks
    private static readonly NonClosableMemoryStream SourceStream = new NonClosableMemoryStream();

    // Pre-allocate a large buffer to avoid resizing during the benchmark
    private static readonly NonClosableMemoryStream DestinationStream = new NonClosableMemoryStream(capacity: 4 * 1024 * 1024);

    // Ensure consistent output quality across all libraries
    private const int OutputQuality = 75;

    private static readonly JpegEncoder JpegImageSharpEncoder = new JpegEncoder
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
    }

    [Benchmark(Baseline = true)]
    public async Task ImageSharpResize()
    {
        ResetStreams();

        using var image = await Image.LoadAsync(SourceStream);
        image.Mutate(static x => x.Resize(OutputWidth, OutputHeight));

        await image.SaveAsync(DestinationStream, JpegImageSharpEncoder);
    }

    [Benchmark]
    public async Task MagickNetResize()
    {
        ResetStreams();

        using var image = new MagickImage(SourceStream);
        image.Quality = OutputQuality;
        image.Resize(MagickOutputSize);

        await image.WriteAsync(DestinationStream);
    }

    [Benchmark]
    public void NetVipsResize()
    {
        ResetStreams();

        using var image = NetVips.Image.NewFromStream(SourceStream);
        image.ThumbnailImage(width: OutputWidth, height: OutputHeight)
            .JpegsaveStream(DestinationStream, q: OutputQuality);
    }

    [Benchmark]
    public void SkiaSharpResize()
    {
        ResetStreams();

        using var image = SKBitmap.Decode(SourceStream);
        using var bitmap = new SKBitmap(OutputWidth, OutputHeight);
        using var canvas = new SKCanvas(bitmap);

        canvas.DrawBitmap(image, new SKRect(0, 0, OutputWidth, OutputHeight));

        bitmap.Encode(DestinationStream, SKEncodedImageFormat.Jpeg, OutputQuality);
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