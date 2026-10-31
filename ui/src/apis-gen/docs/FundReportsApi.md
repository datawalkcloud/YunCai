# FundReportsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdFundReportsExportGet**](FundReportsApi.md#accountsetsaccountsetidfundreportsexportget) | **GET** /accountsets/{accountSetId}/fund-reports/export |  |
| [**accountsetsAccountSetIdFundReportsGet**](FundReportsApi.md#accountsetsaccountsetidfundreportsget) | **GET** /accountsets/{accountSetId}/fund-reports |  |
| [**accountsetsAccountSetIdFundReportsReconciliationExportGet**](FundReportsApi.md#accountsetsaccountsetidfundreportsreconciliationexportget) | **GET** /accountsets/{accountSetId}/fund-reports/reconciliation/export |  |
| [**accountsetsAccountSetIdFundReportsReconciliationGet**](FundReportsApi.md#accountsetsaccountsetidfundreportsreconciliationget) | **GET** /accountsets/{accountSetId}/fund-reports/reconciliation |  |



## accountsetsAccountSetIdFundReportsExportGet

> accountsetsAccountSetIdFundReportsExportGet(accountSetId, dimension, startDate, endDate, journalId, keyword, secondary)



### Example

```ts
import {
  Configuration,
  FundReportsApi,
} from '';
import type { AccountsetsAccountSetIdFundReportsExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FundReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    dimension: dimension_example,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    journalId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    keyword: keyword_example,
    // string (optional)
    secondary: secondary_example,
  } satisfies AccountsetsAccountSetIdFundReportsExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdFundReportsExportGet(body);
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
| **dimension** | `string` |  | [Optional] [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **journalId** | `string` |  | [Optional] [Defaults to `undefined`] |
| **keyword** | `string` |  | [Optional] [Defaults to `undefined`] |
| **secondary** | `string` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdFundReportsGet

> FundReportDto accountsetsAccountSetIdFundReportsGet(accountSetId, dimension, startDate, endDate, journalId, keyword, secondary)



### Example

```ts
import {
  Configuration,
  FundReportsApi,
} from '';
import type { AccountsetsAccountSetIdFundReportsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FundReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    dimension: dimension_example,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    journalId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    keyword: keyword_example,
    // string (optional)
    secondary: secondary_example,
  } satisfies AccountsetsAccountSetIdFundReportsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdFundReportsGet(body);
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
| **dimension** | `string` |  | [Optional] [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **journalId** | `string` |  | [Optional] [Defaults to `undefined`] |
| **keyword** | `string` |  | [Optional] [Defaults to `undefined`] |
| **secondary** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**FundReportDto**](FundReportDto.md)

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


## accountsetsAccountSetIdFundReportsReconciliationExportGet

> accountsetsAccountSetIdFundReportsReconciliationExportGet(accountSetId, date, journalId)



### Example

```ts
import {
  Configuration,
  FundReportsApi,
} from '';
import type { AccountsetsAccountSetIdFundReportsReconciliationExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FundReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    date: 2013-10-20,
    // string (optional)
    journalId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdFundReportsReconciliationExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdFundReportsReconciliationExportGet(body);
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
| **date** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **journalId** | `string` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdFundReportsReconciliationGet

> FundReconciliationDto accountsetsAccountSetIdFundReportsReconciliationGet(accountSetId, date, journalId)



### Example

```ts
import {
  Configuration,
  FundReportsApi,
} from '';
import type { AccountsetsAccountSetIdFundReportsReconciliationGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FundReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    date: 2013-10-20,
    // string (optional)
    journalId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdFundReportsReconciliationGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdFundReportsReconciliationGet(body);
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
| **date** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **journalId** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**FundReconciliationDto**](FundReconciliationDto.md)

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

