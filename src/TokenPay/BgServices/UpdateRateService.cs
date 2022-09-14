using Flurl;
using Flurl.Http;
using FreeSql;
using TokenPay.Domains;
using TokenPay.Helper;

namespace TokenPay.BgServices
{
    public class UpdateRateService : BaseScheduledService
    {
        const string baseUrl = "https://www.okx.com";
        const string User_Agent = "TokenPay/1.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36";
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UpdateRateService> _logger;
        private readonly FlurlClient client;
        public UpdateRateService(
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            ILogger<UpdateRateService> logger) : base("更新汇率", TimeSpan.FromSeconds(3600), logger)
        {
            this._configuration = configuration;
            this._serviceProvider = serviceProvider;
            this._logger = logger;
            var WebProxy = configuration.GetValue<string>("WebProxy");
            client = new FlurlClient();
            client.Settings.Timeout = TimeSpan.FromSeconds(5);
            if (!string.IsNullOrEmpty(WebProxy))
            {
                client.Settings.HttpClientFactory = new ProxyHttpClientFactory(WebProxy);
            }

        }

        protected override async Task ExecuteAsync()
        {
            var rate1 = _configuration.GetValue("Rate:USDT", 0m);
            var rate2 = _configuration.GetValue("Rate:TRX", 0m);
            if (rate1 > 0 && rate2 > 0)
            {
                // 无需更新汇率
                return;
            }
            _logger.LogInformation("------------------{tips}------------------", "开始更新汇率");
            using IServiceScope scope = _serviceProvider.CreateScope();
            var _repository = scope.ServiceProvider.GetRequiredService<IBaseRepository<TokenRate>>();

            var list = new List<TokenRate>();
            var side = "buy";

            if (rate1 <= 0)
            {
                try
                {
                    var result = await baseUrl
                        .WithClient(client)
                        .WithHeaders(new { User_Agent = User_Agent })
                        .AppendPathSegment("/v3/c2c/otc-ticker/quotedPrice")
                        .SetQueryParams(new
                        {
                            side = side,
                            quoteCurrency = FiatCurrency.CNY.ToString(),
                            baseCurrency = "USDT",
                        })
                        .GetJsonAsync<Root>();
                    if (result.code == 0)
                    {
                        list.Add(new TokenRate
                        {
                            Id = $"{Currency.USDT_TRC20}_{FiatCurrency.CNY}",
                            Currency = Currency.USDT_TRC20,
                            FiatCurrency = FiatCurrency.CNY,
                            LastUpdateTime = DateTime.Now,
                            Rate = result.data.First(x => x.bestOption).price,
                        });
                    }
                    else
                    {
                        _logger.LogWarning("USDT 汇率获取失败！错误信息：{msg}", result.msg ?? result.error_message);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning("USDT 汇率获取失败！错误信息：{msg}", e?.InnerException?.Message + "; " + e?.Message);
                }
            }
            if (rate2 <= 0)
            {
                try
                {
                    var result = await baseUrl
                        .WithClient(client)
                        .WithHeaders(new { User_Agent = User_Agent })
                        .AppendPathSegment("/v3/c2c/otc-ticker/quotedPrice")
                        .SetQueryParams(new
                        {
                            side = side,
                            quoteCurrency = FiatCurrency.CNY.ToString(),
                            baseCurrency = "TRX",
                        })
                        .GetJsonAsync<Root>();
                    if (result.code == 0)
                    {
                        list.Add(new TokenRate
                        {
                            Id = $"{Currency.TRX}_{FiatCurrency.CNY}",
                            Currency = Currency.TRX,
                            FiatCurrency = FiatCurrency.CNY,
                            LastUpdateTime = DateTime.Now,
                            Rate = result.data.First(x => x.bestOption).price,
                        });
                    }
                    else
                    {
                        _logger.LogWarning("USDT 汇率获取失败！错误信息：{msg}", result.msg ?? result.error_message);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning("USDT 汇率获取失败！错误信息：{msg}", e?.InnerException?.Message + "; " + e?.Message);
                }
            }
            foreach (var item in list)
            {
                _logger.LogInformation("更新汇率，{a}=>{b} = {c}", item.Currency, item.FiatCurrency, item.Rate);
                await _repository.InsertOrUpdateAsync(item);
            }
            _logger.LogInformation("------------------{tips}------------------", "结束更新汇率");
        }
    }

    class Datum
    {
        public bool bestOption { get; set; }
        public string payment { get; set; }
        public decimal price { get; set; }
    }

    class Root
    {
        public int code { get; set; }
        public List<Datum> data { get; set; }
        public string detailMsg { get; set; }
        public string error_code { get; set; }
        public string error_message { get; set; }
        public string msg { get; set; }
    }

    enum OkxSide
    {
        Buy,
        Sell
    }

}
