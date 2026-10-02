using System.Net;
using System.Text;
using System.Text.Json;

namespace AuthClientLab
{
    public class Program
    {
        private const string ServerUrl = "https://localhost:5001";

        // Main은 일반적인 콘솔 시작 형식으로 비동기 작업을 끝까지 기다립니다.
        private static void Main(string[] args)
        {
            RunAsync().GetAwaiter().GetResult();
        }

        private static async Task RunAsync()
        {
            Console.Write("아이디: ");
            string id = Console.ReadLine();
            Console.Write("비밀번호: ");
            string password = Console.ReadLine();

            LoginRequest loginRequest = new LoginRequest
            {
                Id = id,
                Password = password
            };

            // JsonSerializer는 C# 객체를 JSON 문자열로 바꾸는 도구입니다.
            string loginRequestJson = JsonSerializer.Serialize(loginRequest);

            // HttpClient는 HTTP/HTTPS 요청을 보내는 객체입니다.
            using (HttpClient client = new())
            {
                HttpResponseMessage response = await PostJsonAsync(client, "/login", loginRequestJson);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"로그인 성공!");
                    await PrintLoginResponseAsync(response);
                }
                // NotFound(404)는 서버 DB에 해당 아이디가 없다는 뜻입니다.
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.Write("등록되지 않은 아이디입니다. 이 정보로 회원가입 하시겠습니까? (y/n): ");
                    string answer = Console.ReadLine();
                    if (answer != "y" && answer != "Y")
                    {
                        Console.WriteLine("회원가입을 취소했습니다.");
                        return;
                    }

                    HttpResponseMessage registerResponse = await PostJsonAsync(client, "/register", loginRequestJson);
                    if (registerResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine("회원가입 성공!");
                        await PrintLoginResponseAsync(registerResponse);
                    }
                    else
                    {
                        Console.WriteLine("회원가입 실패");
                    }
                }
                else
                {
                    Console.WriteLine("로그인 실패");
                }
            }
        }

        private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, string json)
        {
            // StringContent는 문자열을 HTTP 요청 본문으로 담는 객체입니다.
            using (StringContent content = new(json, Encoding.UTF8, "application/json"))
            {
                // PostAsync는 지정한 주소로 JSON POST 요청을 비동기로 보냅니다.
                return await client.PostAsync(ServerUrl + path, content);
            }
        }

        private static async Task PrintLoginResponseAsync(HttpResponseMessage response)
        {
            string responseJson = await response.Content.ReadAsStringAsync();
            LoginResponse loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Console.WriteLine($"PlayerId: {loginResponse.PlayerId}");
            Console.WriteLine($"AccessToken: {loginResponse.AccessToken}");
        }
    }

    // LoginRequest는 서버에 보낼 로그인 JSON 구조입니다.
    public class LoginRequest
    {
        public string Id { get; set; }
        public string Password { get; set; }
    }

    // LoginResponse는 서버가 보낸 로그인 JSON 구조입니다.
    public class LoginResponse
    {
        public string AccessToken { get; set; }
        public int PlayerId { get; set; }
    }

}