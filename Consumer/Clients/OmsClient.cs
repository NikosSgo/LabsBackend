using System.Text;
using Common;
using Models.Dto.V1.Requests;
using Models.Dto.V1.Responses;

namespace Consumer.Clients;

public class OmsClient(HttpClient client)
{
    public async Task<V1AuditLogOrderResponse> LogOrder(V1AuditLogOrderRequest request, CancellationToken token)
    {
        try
        {
            var jsonContent = request.ToJson();
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var msg = await client.PostAsync("api/v1/audit/log-order", content, token);
            
            if (msg.IsSuccessStatusCode)
            {
                var responseContent = await msg.Content.ReadAsStringAsync(cancellationToken: token);
                return responseContent.FromJson<V1AuditLogOrderResponse>();
            }

            var errorContent = await msg.Content.ReadAsStringAsync(cancellationToken: token);
            throw new HttpRequestException($"OMS API returned error: {msg.StatusCode} - {errorContent}");
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"Failed to send audit log request to OMS: {ex.Message}", ex);
        }
    }
}
