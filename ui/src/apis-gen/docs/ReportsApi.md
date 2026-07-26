# ReportsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdReportsBalanceSheetGet**](ReportsApi.md#accountsetsaccountsetidreportsbalancesheetget) | **GET** /accountsets/{accountSetId}/Reports/balance-sheet |  |
| [**accountsetsAccountSetIdReportsCashJournalGet**](ReportsApi.md#accountsetsaccountsetidreportscashjournalget) | **GET** /accountsets/{accountSetId}/Reports/cash-journal |  |
| [**accountsetsAccountSetIdReportsIncomeStatementGet**](ReportsApi.md#accountsetsaccountsetidreportsincomestatementget) | **GET** /accountsets/{accountSetId}/Reports/income-statement |  |
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

