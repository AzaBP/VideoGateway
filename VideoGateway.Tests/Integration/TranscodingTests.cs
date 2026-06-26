using Xunit;
using VideoGateway.Tests.Integration.Helpers;

namespace VideoGateway.Tests.Integration;

[Trait("Category", "Integration")]
public class TranscodingTests : IDisposable
{
    private readonly TempDirHelper _tempDir;
    private readonly string _samplesDir;

    public TranscodingTests()
    {
        _tempDir = new TempDirHelper("Transcoding");
        _samplesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples");
    }

    public void Dispose() => _tempDir.Dispose();

    private string GetShortestH264Sample()
    {
        return Directory.GetFiles(_samplesDir, "*.mp4")
            .OrderBy(f => new FileInfo(f).Length)
            .First(f => FfprobeHelper.DetectCodec(f) == "h264");
    }

    private string TranscodeToFile(string inputFile, string extraArgs = "")
    {
        var outputFile = _tempDir.GetTempFilePath(".mp4");
        var args = "-y -i \"" + inputFile + "\" -c:v libx264 -preset ultrafast -tune zerolatency -c:a aac " + extraArgs + " \"" + outputFile + "\"";
        var (exitCode, stderr, _) = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(60));
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputFile));
        return outputFile;
    }

    [Fact] public void Transcode_H264_Mp4_ToH264() { TranscodeToFile(GetShortestH264Sample()); }
    [Fact] public void Transcode_H264_Mp4_OutputIsH264() { var o = TranscodeToFile(GetShortestH264Sample()); Assert.Equal("h264", FfprobeHelper.DetectCodec(o)); }
    [Fact] public void Transcode_AV1_Mp4_ToH264() { var f = Directory.GetFiles(_samplesDir, "*.mp4").FirstOrDefault(f => FfprobeHelper.DetectCodec(f) == "av1"); Assert.NotNull(f); TranscodeToFile(f!); }
    [Fact] public void Transcode_AV1_Mp4_OutputIsH264() { var f = Directory.GetFiles(_samplesDir, "*.mp4").FirstOrDefault(f => FfprobeHelper.DetectCodec(f) == "av1"); Assert.NotNull(f); var o = TranscodeToFile(f!); Assert.Equal("h264", FfprobeHelper.DetectCodec(o)); }
    [Fact] public void Transcode_H265_Mkv_ToH264() { TranscodeToFile(SyntheticFileGenerator.GenerateH265Mkv(_tempDir.DirectoryPath, 2)); }
    [Fact] public void Transcode_H265_Mkv_OutputIsH264() { var o = TranscodeToFile(SyntheticFileGenerator.GenerateH265Mkv(_tempDir.DirectoryPath, 2)); Assert.Equal("h264", FfprobeHelper.DetectCodec(o)); }
    [Fact] public void Transcode_Mjpeg_Avi_ToH264() { TranscodeToFile(SyntheticFileGenerator.GenerateMjpegAvi(_tempDir.DirectoryPath, 2)); }
    [Fact] public void Transcode_Mjpeg_Avi_OutputIsH264() { var o = TranscodeToFile(SyntheticFileGenerator.GenerateMjpegAvi(_tempDir.DirectoryPath, 2)); Assert.Equal("h264", FfprobeHelper.DetectCodec(o)); }
    [Fact] public void Transcode_Mov_H264_ToH264() { TranscodeToFile(SyntheticFileGenerator.GenerateH264Mov(_tempDir.DirectoryPath, 2)); }

    [Fact]
    public void Transcode_H264_Elem_ToH264()
    {
        var elemFile = SyntheticFileGenerator.GenerateH264Elementary(_tempDir.DirectoryPath, 2);
        var outputFile = _tempDir.GetTempFilePath(".mp4");
        var args = "-y -i \"" + elemFile + "\" -c:v libx264 -preset ultrafast -tune zerolatency \"" + outputFile + "\"";
        var (exitCode, _, _) = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(30));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Transcode_H265_Elem_ToH264()
    {
        var elemFile = SyntheticFileGenerator.GenerateH265Elementary(_tempDir.DirectoryPath, 2);
        var outputFile = _tempDir.GetTempFilePath(".mp4");
        var args = "-y -i \"" + elemFile + "\" -c:v libx264 -preset ultrafast -tune zerolatency \"" + outputFile + "\"";
        var (exitCode, _, _) = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(30));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Transcode_Mjpeg_Seq_ToH264()
    {
        var mjpegFile = SyntheticFileGenerator.GenerateMjpegSequence(_tempDir.DirectoryPath, 2);
        var outputFile = _tempDir.GetTempFilePath(".mp4");
        var args = "-y -f mjpeg -i \"" + mjpegFile + "\" -c:v libx264 -preset ultrafast -tune zerolatency \"" + outputFile + "\"";
        var (exitCode, _, _) = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(30));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Transcode_Raw_Bgr24_ToH264()
    {
        var rawFile = SyntheticFileGenerator.GenerateRawVideo(_tempDir.DirectoryPath, 2);
        var outputFile = _tempDir.GetTempFilePath(".mp4");
        var args = "-y -f rawvideo -pix_fmt bgr24 -s 160x120 -i \"" + rawFile + "\" -c:v libx264 -preset ultrafast -tune zerolatency \"" + outputFile + "\"";
        var (exitCode, _, _) = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(30));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Transcode_OutputDuration_MatchesInput()
    {
        var sample = GetShortestH264Sample();
        var inputDuration = FfprobeHelper.GetDuration(sample);
        var output = TranscodeToFile(sample);
        var outputDuration = FfprobeHelper.GetDuration(output);
        Assert.InRange(outputDuration, inputDuration - 1, inputDuration + 1);
    }

    [Fact]
    public void Transcode_OutputResolution_MatchesInput()
    {
        var sample = GetShortestH264Sample();
        var (inW, inH, _) = FfprobeHelper.GetStreamInfo(sample);
        var output = TranscodeToFile(sample);
        var (outW, outH, _) = FfprobeHelper.GetStreamInfo(output);
        Assert.Equal(inW, outW);
        Assert.Equal(inH, outH);
    }

    [Fact]
    public void Transcode_OutputFps_IsReasonable()
    {
        var sample = GetShortestH264Sample();
        var output = TranscodeToFile(sample);
        var (_, _, fps) = FfprobeHelper.GetStreamInfo(output);
        Assert.InRange(fps, 1, 120);
    }

    [Fact]
    public void Transcode_Ultrafast_CompletesQuickly()
    {
        var sample = GetShortestH264Sample();
        var outputFile = _tempDir.GetTempFilePath(".mp4");
        var args = "-y -i \"" + sample + "\" -c:v libx264 -preset ultrafast -tune zerolatency -c:a aac \"" + outputFile + "\"";
        var (_, _, duration) = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(60));
        Assert.True(duration.TotalSeconds < 30);
    }

    [Fact]
    public void Transcode_OutputFileSize_IsNonZero()
    {
        var sample = GetShortestH264Sample();
        var output = TranscodeToFile(sample);
        Assert.True(new FileInfo(output).Length > 0);
    }

    [Fact]
    public void Transcode_OutputFileSize_IsReasonable()
    {
        var sample = GetShortestH264Sample();
        var inputSize = new FileInfo(sample).Length;
        var output = TranscodeToFile(sample);
        Assert.True(new FileInfo(output).Length < inputSize * 3);
    }

    [Fact]
    public void Transcode_NullOutput_Completes()
    {
        var sample = GetShortestH264Sample();
        var args = "-y -i \"" + sample + "\" -c:v libx264 -preset ultrafast -tune zerolatency -f null -";
        var (exitCode, _, _) = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(30));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Transcode_AllSamples_Succeed()
    {
        var sampleFiles = Directory.GetFiles(_samplesDir, "*.mp4");
        foreach (var file in sampleFiles)
        {
            var outputFile = _tempDir.GetTempFilePath(".mp4");
            var args = "-y -i \"" + file + "\" -c:v libx264 -preset ultrafast -tune zerolatency -c:a aac \"" + outputFile + "\"";
            var (exitCode, _, _) = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(60));
            Assert.True(exitCode == 0);
        }
    }
}
