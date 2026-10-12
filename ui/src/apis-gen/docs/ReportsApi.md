# ReportsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdReportsBalanceSheetGet**](ReportsApi.md#accountsetsaccountsetidreportsbalancesheetget) | **GET** /accountsets/{accountSetId}/Reports/balance-sheet |  |
| [**accountsetsAccountSetIdReportsCashFlowGet**](ReportsApi.md#accountsetsaccountsetidreportscashflowget) | **GET** /accountsets/{accountSetId}/Reports/cash-flow |  |
| [**accountsetsAccountSetIdReportsCashFlowQuarterGet**](ReportsApi.md#accountsetsaccountsetidreportscashflowquarterget) | **GET** /accountsets/{accountSetId}/Reports/cash-flow-quarter |  |
| [**accountsetsAccountSetIdReportsCashJournalGet**](ReportsApi.md#accountsetsaccountsetidreportscashjournalget) | **GET** /accountsets/{accountSetId}/Reports/cash-journal |  |
| [**accountsetsAccountSetIdReportsDepartmentIncomeGet**](ReportsApi.md#accountsetsaccountsetidreportsdepartmentincomeget) | **GET** /accountsets/{accountSetId}/Reports/department-income |  |
| [**accountsetsAccountSetIdReportsEquityChangeGet**](ReportsApi.md#accountsetsaccountsetidreportsequitychangeget) | **GET** /accountsets/{accountSetId}/Reports/equity-change |  |
| [**accountsetsAccountSetIdReportsExpenseDetailGet**](ReportsApi.md#accountsetsaccountsetidreportsexpensedetailget) | **GET** /accountsets/{accountSetId}/Reports/expense-detail |  |
| [**accountsetsAccountSetIdReportsExportGet**](ReportsApi.md#accountsetsaccountsetidreportsexportget) | **GET** /accountsets/{accountSetId}/Reports/export |  |
| [**accountsetsAccountSetIdReportsFinancialSummaryGet**](ReportsApi.md#accountsetsaccountsetidreportsfinancialsummaryget) | **GET** /accountsets/{accountSetId}/Reports/financial-summary |  |
| [**accountsetsAccountSetIdReportsIncomeQuarterGet**](ReportsApi.md#accountsetsaccountsetidreportsincomequarterget) | **GET** /accountsets/{accountSetId}/Reports/income-quarter |  |
| [**accountsetsAccountSetIdReportsIncomeStatementGet**](ReportsApi.md#accountsetsaccountsetidreportsincomestatementget) | **GET** /accountsets/{accountSetId}/Reports/income-statement |  |
| [**accountsetsAccountSetIdReportsProjectIncomeGet**](ReportsApi.md#accountsetsaccountsetidreportsprojectincomeget) | **GET** /accountsets/{accountSetId}/Reports/project-income |  |
| [**accountsetsAccountSetIdReportsTaxGet**](ReportsApi.md#accountsetsaccountsetidreportstaxget) | **GET** /accountsets/{accountSetId}/Reports/tax |  |
| [**accountsetsAccountSetIdReportsTrialBalanceGet**](ReportsApi.md#accountsetsaccountsetidreportstrialbalanceget) | **GET** /accountsets/{accountSetId}/Reports/trial-balance |  |



## accountsetsAccountSetIdReportsBalanceSheetGet

> BalanceSheetDto accountsetsAccountSetIdReportsBalanceSheetGet(accountSetId, year, month)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsBalanceSheetGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
  } satisfies AccountsetsAccountSetIdReportsBalanceSheetGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsBalanceSheetGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **month** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**BalanceSheetDto**](BalanceSheetDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdReportsCashFlowGet

> CashFlowDto accountsetsAccountSetIdReportsCashFlowGet(accountSetId, year, month)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsCashFlowGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
  } satisfies AccountsetsAccountSetIdReportsCashFlowGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsCashFlowGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **month** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**CashFlowDto**](CashFlowDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdReportsCashFlowQuarterGet

> CashFlowQuarterDto accountsetsAccountSetIdReportsCashFlowQuarterGet(accountSetId, year, quarter)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsCashFlowQuarterGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    quarter: 56,
  } satisfies AccountsetsAccountSetIdReportsCashFlowQuarterGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsCashFlowQuarterGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **quarter** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**CashFlowQuarterDto**](CashFlowQuarterDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdReportsCashJournalGet

> CashJournalDto accountsetsAccountSetIdReportsCashJournalGet(accountSetId, startDate, endDate)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsCashJournalGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
  } satisfies AccountsetsAccountSetIdReportsCashJournalGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsCashJournalGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**CashJournalDto**](CashJournalDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdReportsDepartmentIncomeGet

> DepartmentIncomeDto accountsetsAccountSetIdReportsDepartmentIncomeGet(accountSetId, year, month)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsDepartmentIncomeGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
  } satisfies AccountsetsAccountSetIdReportsDepartmentIncomeGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsDepartmentIncomeGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **month** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**DepartmentIncomeDto**](DepartmentIncomeDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdReportsEquityChangeGet

> EquityChangeDto accountsetsAccountSetIdReportsEquityChangeGet(accountSetId, year)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsEquityChangeGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
  } satisfies AccountsetsAccountSetIdReportsEquityChangeGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsEquityChangeGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**EquityChangeDto**](EquityChangeDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdReportsExpenseDetailGet

> ExpenseDetailDto accountsetsAccountSetIdReportsExpenseDetailGet(accountSetId, year, month)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsExpenseDetailGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
  } satisfies AccountsetsAccountSetIdReportsExpenseDetailGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsExpenseDetailGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **month** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**ExpenseDetailDto**](ExpenseDetailDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdReportsExportGet

> accountsetsAccountSetIdReportsExportGet(accountSetId, kind, year, month, quarter)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    kind: kind_example,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
    // number (optional)
    quarter: 56,
  } satisfies AccountsetsAccountSetIdReportsExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsExportGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **kind** | `string` |  | [Optional] [Defaults to `undefined`] |
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **month** | `number` |  | [Optional] [Defaults to `undefined`] |
| **quarter** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

`void` (Empty response body)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: Not defined


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdReportsFinancialSummaryGet

> FinancialSummaryDto accountsetsAccountSetIdReportsFinancialSummaryGet(accountSetId, year, month)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsFinancialSummaryGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
  } satisfies AccountsetsAccountSetIdReportsFinancialSummaryGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsFinancialSummaryGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **month** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**FinancialSummaryDto**](FinancialSummaryDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdReportsIncomeQuarterGet

> IncomeStatementQuarterDto accountsetsAccountSetIdReportsIncomeQuarterGet(accountSetId, year, quarter)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsIncomeQuarterGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    quarter: 56,
  } satisfies AccountsetsAccountSetIdReportsIncomeQuarterGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsIncomeQuarterGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **quarter** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**IncomeStatementQuarterDto**](IncomeStatementQuarterDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdReportsIncomeStatementGet

> IncomeStatementDto accountsetsAccountSetIdReportsIncomeStatementGet(accountSetId, year, month)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsIncomeStatementGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
  } satisfies AccountsetsAccountSetIdReportsIncomeStatementGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsIncomeStatementGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **month** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**IncomeStatementDto**](IncomeStatementDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdReportsProjectIncomeGet

> ProjectIncomeDto accountsetsAccountSetIdReportsProjectIncomeGet(accountSetId, year, month)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsProjectIncomeGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
  } satisfies AccountsetsAccountSetIdReportsProjectIncomeGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsProjectIncomeGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **month** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**ProjectIncomeDto**](ProjectIncomeDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdReportsTaxGet

> TaxStatisticsDto accountsetsAccountSetIdReportsTaxGet(accountSetId, year, month)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsTaxGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
  } satisfies AccountsetsAccountSetIdReportsTaxGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsTaxGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **month** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**TaxStatisticsDto**](TaxStatisticsDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdReportsTrialBalanceGet

> TrialBalanceDto accountsetsAccountSetIdReportsTrialBalanceGet(accountSetId, startDate, endDate)



### Example

```ts
import {
  Configuration,
  ReportsApi,
} from '';
import type { AccountsetsAccountSetIdReportsTrialBalanceGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
  } satisfies AccountsetsAccountSetIdReportsTrialBalanceGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdReportsTrialBalanceGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**TrialBalanceDto**](TrialBalanceDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)

