using Dapper;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Server.Common
{
    public static partial class ExtendsMethod
    {
        public static async Task<int> ExecuteAsync(this MySqlTransaction transaction, string query)
        {
            return await transaction.Connection.ExecuteAsync(query, transaction: transaction);
        }

        public static DynamicParameters AddInputInt(this DynamicParameters param, string name, int value)
        {
            param.Add(name, value, DbType.Int32, ParameterDirection.Input);
            return param;
        }

        public static DynamicParameters AddInputString(this DynamicParameters param, string name, string value)
        {
            param.Add(name, value, DbType.String, ParameterDirection.Input, size: 128);
            return param;
        }
        /*
        public static DynamicParameters AddOutputInt(this DynamicParameters param, string name, int value)
        {
            param.Add(name, value, DbType.Int32, ParameterDirection.Output);
            return param;
        }

        public static DynamicParameters AddOutputString(this DynamicParameters param, string name, string value)
        {
            param.Add(name, value, DbType.String, ParameterDirection.Output, size: 128);
            return param;
        }
        */
    }

    public class AccountDbModule : MySqlModule
    {
        public AccountDbModule(string connectionString, ILogger<AccountDbModule> logger) : base(connectionString, logger) { }
    }

    public class GameDbModule : MySqlModule
    {
        public GameDbModule(string connectionString, ILogger<GameDbModule> logger) : base(connectionString, logger) { }
    }

    public class MySqlModule
    {
        private readonly ILogger _logger;
        private readonly string _connectionString;
        public MySqlModule(string connectString, ILogger logger)
        {
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
            _connectionString = connectString;
            _logger = logger;
            /*
            try
            {
                // 'using'을 사용하여 MySqlConnection 객체가 반드시 닫히고 해제되도록 보장합니다.
                using (var connection = new MySqlConnection(_connectionString))
                {
                    // 데이터베이스에 연결을 시도합니다.
                    connection.Open();

                    // 성공적으로 연결되면 즉시 닫습니다. 확인이 목적이기 때문입니다.
                    connection.Close();
                }
            }
            catch (MySqlException ex)
            {
                // MySqlException은 인증 실패, 서버 연결 불가 등 구체적인 DB 오류를 포함합니다.
                // 이 예외를 다시 던져서 모듈 생성 실패를 명확히 알립니다.
                throw new InvalidOperationException($"Failed to connect to MySQL. Please check the connection string. Inner Exception: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                // 그 외 예기치 못한 오류 처리
                throw new InvalidOperationException($"An unexpected error occurred while trying to connect to MySQL. Inner Exception: {ex.Message}", ex);
            }
            */
        }

        public async Task<(bool, T?)> QuerySingleAsync<T>(string query)
        {
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                var result = await connection.QuerySingleAsync<T>(query);
                return (true, result);
            }
            catch (InvalidOperationException) // 결과 개수 문제 예외
            {
                _logger.Error("쿼리 결과가 하나가 아닙니다. Query: {Query}", query);
                return (false, default);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MySQL operation failed");
                return (false, default);
            }
        }

        public async Task<(bool, IEnumerable<T>?)> QueryMultiAsync<T>(string query)
        {
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                var result = await connection.QueryAsync<T>(query);
                return (true, result);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MySQL operation failed");
                return (false, null);
            }
        }
        /*
        public async Task QueryMultiAsync2<T>(string query)
        {
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync(); // 명시한다

                await foreach (var row in connection.QueryUnbufferedAsync<T>("select 'abc' as [Value] union all select @txt", new { txt = "def" }))
                {
                    //T value = row;
                    //results.Add(value);
                }
                return ;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MySQL operation failed");
                return;
            }
        }
        */

        public async Task<bool> ExecAsync(string query)
        {
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                int rows = await connection.ExecuteAsync(query);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MySQL operation failed");
                return false;
            }
        }

        public async Task<bool> TransactionAsync(List<string> queries)
        {
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync(); // 명시한다
                await using var transaction = await connection.BeginTransactionAsync();
                try
                {
                    foreach (var query in queries)
                    {
                        await transaction.ExecuteAsync(query);
                    }
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.Error(ex, "MySQL operation failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MySQL operation failed");
                return false;
            }
        }

        private static void SetProcedureBaseOutParam(DynamicParameters p)
        {
            p.Add("o_success", dbType: DbType.Byte, direction: ParameterDirection.Output);
            p.Add("o_error_code", dbType: DbType.AnsiStringFixedLength, size: 5, direction: ParameterDirection.Output);
            p.Add("o_error_msg", dbType: DbType.String, direction: ParameterDirection.Output);
        }

        private bool GetProcedureSuccess(DynamicParameters p)
        {
            var successByte = p.Get<byte?>("o_success") ?? 1;
            bool success = successByte != 0; // TINYINT(1) -> bool 변환
            if (!success)
            {
                var errorCode = p.Get<string>("o_error_code")?.TrimEnd(); // CHAR(5)면 공백 패딩 제거
                var errorMsg = p.Get<string>("o_error_msg");
                _logger.Error("success={Success}, code={Code}, msg={Msg}", success, errorCode, errorMsg);
            }
            return success;
        }

        public async Task<bool> CallProcedureExec(string proc, DynamicParameters p)
        {
            try
            {
                // OUT 파라미터
                SetProcedureBaseOutParam(p);

                await using var connection = new MySqlConnection(_connectionString);
                int result = await connection.ExecuteAsync(proc, p, commandType: CommandType.StoredProcedure);
                return GetProcedureSuccess(p);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MySQL operation failed");
                return false;
            }
        }
        public async Task<(bool, T?)> CallProcedureQuerySingle<T>(string proc, DynamicParameters p)
        {
            try
            {
                // OUT 파라미터
                SetProcedureBaseOutParam(p);

                await using var connection = new MySqlConnection(_connectionString);
                var result = await connection.QueryFirstOrDefaultAsync<T>(proc, p, commandType: CommandType.StoredProcedure);
                return (GetProcedureSuccess(p), result);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MySQL operation failed");
                return (false, default);
            }
        }
        public async Task<(bool, List<T>?)> CallProcedureQueryMultiple<T>(string proc, DynamicParameters p)
        {
            try
            {
                // OUT 파라미터
                SetProcedureBaseOutParam(p);

                await using var connection = new MySqlConnection(_connectionString);
                using var multi = await connection.QueryMultipleAsync(proc, p, commandType: CommandType.StoredProcedure);
                // 2. [중요!] SELECT 결과 셋을 먼저 읽습니다.
                // 로그인 성공 시 하나의 사용자 정보가 반환되므로 ReadFirstOrDefault 사용
                var results = await multi.ReadAsync<T>();
                return (GetProcedureSuccess(p), results?.AsList());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MySQL operation failed");
                return (false, default);
            }
        }
    }
}
