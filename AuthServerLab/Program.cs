using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;

namespace AuthServerLab
{
    internal class Program
    {
        private static readonly ConcurrentDictionary<string, int> IssuedTokens = new();

        // PasswordHasher는 비밀번호를 원문 대신 해시로 바꿔 저장하고 비교하는 도구입니다.
        private static readonly PasswordHasher<string> Hasher = new();

        private static string ConnectionString = "";

        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            WebApplication app = builder.Build();

            // ContentRootPath는 서버 프로젝트 폴더이며, User.db는 그 안의 SQLite 파일입니다.
            ConnectionString = "Data Source=" + Path.Combine(app.Environment.ContentRootPath, "User.db");
            CreateUserTable();

            // MapPost는 POST 요청 주소와 처리 메서드를 연결합니다.
            app.MapPost("/login", Login);
            app.MapPost("/register", Register);
            app.MapPost("/validate", Validate);

            // Run은 localhost 5001번 포트에서 HTTPS 서버를 실행합니다.
            app.Run("https://localhost:5001");
        }

        // CreateUserTable은 Users 테이블이 없을 때만 새로 만듭니다.
        private static void CreateUserTable()
        {
            using (SqliteConnection connection = new(ConnectionString))
            {
                connection.Open();
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText =
                        "CREATE TABLE IF NOT EXISTS Users (" +
                        "PlayerId INTEGER PRIMARY KEY AUTOINCREMENT, " +
                        "Id TEXT NOT NULL UNIQUE, " +
                        "PasswordHash TEXT NOT NULL)";
                    command.ExecuteNonQuery();
                }
            }
        }

        private static IResult Login(LoginRequest request)
        {
            // 클라이언트가 입력한 ID or 비밀번호가 없거나 공백으로 왔다면 잘못된 요청으로 반환
            if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrEmpty(request.Password))
            {
                return Results.BadRequest();
            }

            // DB에 없는 아이디면 404를 돌려주고, 클라이언트는 이를 보고 회원가입 여부를 묻습니다.
            if (!TryFindUser(request.Id, out int playerId, out string passwordHash))
            {
                return Results.NotFound();
            }

            // 사용자가 입력한 평문 비밀번호를 Hash로 변환해 저장된 Hash값과 비교했는데 실패했을 때(입력한 비밀번호가 저장된 비밀번호와 다를 때) 
            if (Hasher.VerifyHashedPassword(request.Id, passwordHash, request.Password) == PasswordVerificationResult.Failed)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(IssueToken(playerId));
        }

        private static IResult Register(LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrEmpty(request.Password))
            {
                return Results.BadRequest();
            }

            try
            {
                using (SqliteConnection connection = new(ConnectionString))
                {
                    connection.Open();
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        // $id 같은 매개변수는 입력값이 SQL 문장으로 해석되는 것을 막습니다.
                        command.CommandText =
                            "INSERT INTO Users (Id, PasswordHash) VALUES ($id, $hash); " +
                            "SELECT last_insert_rowid();";
                        command.Parameters.AddWithValue("$id", request.Id);
                        command.Parameters.AddWithValue("$hash", Hasher.HashPassword(request.Id, request.Password));

                        int playerId = Convert.ToInt32(command.ExecuteScalar());
                        return Results.Ok(IssueToken(playerId));
                    }
                }
            }
            // 오류 코드 19는 UNIQUE 제약 위반이며, 이미 가입된 아이디라는 뜻입니다.
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                return Results.Conflict();
            }
        }

        private static IResult Validate(TokenRequest request)
        {
            if (!IssuedTokens.TryGetValue(request.AccessToken, out int playerId))
            {
                return Results.Unauthorized();
            }

            ValidateResponse response = new ValidateResponse
            {
                PlayerId = playerId
            };

            return Results.Ok(response);
        }

        // TryFindUser는 DB에서 아이디를 검색해 PlayerId와 비밀번호 해시를 읽어와 각각 out 주소에 저장
        private static bool TryFindUser(string id, out int playerId, out string passwordHash)
        {
            playerId = 0;
            passwordHash = "";

            using (SqliteConnection connection = new(ConnectionString))
            {
                connection.Open();
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT PlayerId, PasswordHash FROM Users WHERE Id = $id";
                    command.Parameters.AddWithValue("$id", id);

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return false;
                        }

                        playerId = reader.GetInt32(0);
                        passwordHash = reader.GetString(1);
                        return true;
                    }
                }
            }
        }

        // IssueToken은 새 토큰을 발급해 기억해 두고 로그인 응답을 만듭니다.
        private static LoginResponse IssueToken(int playerId)
        {
            string accessToken = Guid.NewGuid().ToString("N");
            IssuedTokens[accessToken] = playerId;

            return new LoginResponse
            {
                IsSuccess = true,
                AccessToken = accessToken,
                PlayerId = playerId
            };
        }
    }

    // LoginRequest는 클라이언트가 보내는 로그인/회원가입 JSON 구조입니다.
    public class LoginRequest
    {
        public string Id { get; set; }
        public string Password { get; set; }
    }

    // LoginResponse는 서버가 돌려주는 로그인 JSON 구조입니다.
    public class LoginResponse
    {
        public bool IsSuccess { get; set; }
        public string AccessToken { get; set; }
        public int PlayerId { get; set; }
    }

    // TokenRequest는 토큰 검증 요청의 JSON 구조입니다.
    public class TokenRequest
    {
        public string AccessToken { get; set; }
    }

    // ValidateResponse는 토큰 검증 성공 뒤 돌려주는 JSON 구조입니다.
    public class ValidateResponse
    {
        public int PlayerId { get; set; }
    }
}
