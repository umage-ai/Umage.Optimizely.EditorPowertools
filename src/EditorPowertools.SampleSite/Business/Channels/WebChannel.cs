using EPiServer.Web;
using Wangkanai.Detection.Models;
using Wangkanai.Detection.Services;

namespace UmageAI.Optimizely.EditorPowerTools.SampleSite.Business.Channels;

/// <summary>
/// Defines the 'Web' content channel
/// </summary>
public class WebChannel : DisplayChannel
{
    public override string ChannelName => "web";

    public override bool IsActive(HttpContext context)
    {
        var detection = context.RequestServices.GetRequiredService<IDetectionService>();
        return detection.Device.Type == Device.Desktop;
    }
}
