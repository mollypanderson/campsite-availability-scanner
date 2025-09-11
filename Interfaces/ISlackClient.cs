using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public interface ISlackClient
{
    Task SendMessageToChannelAsync(string channelId, string text);
}
