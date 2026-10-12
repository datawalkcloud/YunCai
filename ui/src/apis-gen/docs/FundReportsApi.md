# FundReportsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdFundReportsGet**](FundReportsApi.md#accountsetsaccountsetidfundreportsget) | **GET** /accountsets/{accountSetId}/fund-reports |  |
| [**accountsetsAccountSetIdFundReportsReconciliationGet**](FundReportsApi.md#accountsetsaccountsetidfundreportsreconciliationget) | **GET** /accountsets/{accountSetId}/fund-reports/reconciliation |  |



## accountsetsAccountSetIdFundReportsGet

> FundReportDto accountsetsAccountSetIdFundReportsGet(accountSetId, dimension, startDate, endDate)



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


## accountsetsAccountSetIdFundReportsReconciliationGet

> FundReconciliationDto accountsetsAccountSetIdFundReportsReconciliationGet(accountSetId, date)



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

