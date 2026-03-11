using Newtonsoft.Json;
using Pronto.Middleware.Models;

namespace Pronto.Middleware.Models
{
    public class Contract
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public CompanyInfo Company { get; set; }
        public OwnerAffiliation OwnerAffiliation { get; set; }

        public Affiliation Affiliation { get; set; }

        public CustomField Frequency { get; set; }
        public CustomField DSA_Agreement { get; set; }
        [JsonProperty("period_template")]
        public PeriodTemplate PeriodTemplate { get; set; }

        public class CompanyInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }

    public class PeriodTemplate
    {
        [JsonProperty("rate_id")]
        public string RateId { get; set; }

        [JsonProperty("rate_type")]
        public string RateType { get; set; }

        [JsonProperty("account_templates")]
        public object AccountTemplates { get; set; }

        [JsonProperty("include_expense")]
        public int IncludeExpense { get; set; }

        [JsonProperty("allowance_amount")]
        public string AllowanceAmount { get; set; }

        [JsonProperty("allowance_type")]
        public string AllowanceType { get; set; }

        [JsonProperty("budget_type")]
        public string BudgetType { get; set; }

        [JsonProperty("rollover")]
        public string Rollover { get; set; }

        [JsonProperty("include_material")]
        public int IncludeMaterial { get; set; }

        [JsonProperty("amount")]
        public string Amount { get; set; }

        [JsonProperty("rate_charged")]
        public object RateCharged { get; set; }

        [JsonProperty("duration_type")]
        public string DurationType { get; set; }

        [JsonProperty("task_templates")]
        public object TaskTemplates { get; set; }

        [JsonProperty("duration")]
        public string Duration { get; set; }

        [JsonProperty("duration_unit")]
        public string DurationUnit { get; set; }

        [JsonProperty("allowance_time")]
        public string AllowanceTime { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("rate")]
        public PeriodTemplateRate Rate { get; set; }
    }

    public class PeriodTemplateRate
    {
        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("using_allocated_rate")]
        public int UsingAllocatedRate { get; set; }

        [JsonProperty("excess")]
        public int Excess { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }
    }

    public class AcceloResponse
    {
        [JsonProperty("response")]
        public List<ContractResponse> Response { get; set; }

        public class ContractResponse
        {
            public string Id { get; set; }
            public string Title { get; set; }
            [JsonProperty("breadcrumbs")]
            public List<Breadcrumb> Breadcrumbs { get; set; }
            [JsonProperty("owner_affiliation")]
            public string OwnerAffiliationId { get; set; } // Ensure this matches the JSON structure.
            [JsonProperty("period_template")]
            public PeriodTemplate PeriodTemplate { get; set; }
        }

        public class Breadcrumb
        {
            public string Table { get; set; }
            public string Id { get; set; }
            public string Title { get; set; }
        }
    }
}
