# LedgersApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdLedgersAgeingGet**](LedgersApi.md#accountsetsaccountsetidledgersageingget) | **GET** /accountsets/{accountSetId}/Ledgers/ageing |  |
| [**accountsetsAccountSetIdLedgersAssistBalanceGet**](LedgersApi.md#accountsetsaccountsetidledgersassistbalanceget) | **GET** /accountsets/{accountSetId}/Ledgers/assist-balance |  |
| [**accountsetsAccountSetIdLedgersAssistCombineGet**](LedgersApi.md#accountsetsaccountsetidledgersassistcombineget) | **GET** /accountsets/{accountSetId}/Ledgers/assist-combine |  |
| [**accountsetsAccountSetIdLedgersAssistSubsidiaryGet**](LedgersApi.md#accountsetsaccountsetidledgersassistsubsidiaryget) | **GET** /accountsets/{accountSetId}/Ledgers/assist-subsidiary |  |
| [**accountsetsAccountSetIdLedgersGeneralGet**](LedgersApi.md#accountsetsaccountsetidledgersgeneralget) | **GET** /accountsets/{accountSetId}/Ledgers/general |  |
| [**accountsetsAccountSetIdLedgersJournalGet**](LedgersApi.md#accountsetsaccountsetidledgersjournalget) | **GET** /accountsets/{accountSetId}/Ledgers/journal |  |
| [**accountsetsAccountSetIdLedgersKindExportGet**](LedgersApi.md#accountsetsaccountsetidledgerskindexportget) | **GET** /accountsets/{accountSetId}/Ledgers/{kind}/export |  |
| [**accountsetsAccountSetIdLedgersMultiColumnGet**](LedgersApi.md#accountsetsaccountsetidledgersmulticolumnget) | **GET** /accountsets/{accountSetId}/Ledgers/multi-column |  |
| [**accountsetsAccountSetIdLedgersSubsidiaryGet**](LedgersApi.md#accountsetsaccountsetidledgerssubsidiaryget) | **GET** /accountsets/{accountSetId}/Ledgers/subsidiary |  |
| [**accountsetsAccountSetIdLedgersVoucherSummaryGet**](LedgersApi.md#accountsetsaccountsetidledgersvouchersummaryget) | **GET** /accountsets/{accountSetId}/Ledgers/voucher-summary |  |



## accountsetsAccountSetIdLedgersAgeingGet

> AgeingAnalysisDto accountsetsAccountSetIdLedgersAgeingGet(accountSetId, assistType, startDate, endDate, accountFrom, accountTo, assistItemId)



### Example

```ts
import {
  Configuration,
  LedgersApi,
} from '';
import type { AccountsetsAccountSetIdLedgersAgeingGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new LedgersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    assistType: 56,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    accountFrom: accountFrom_example,
    // string (optional)
    accountTo: accountTo_example,
    // string (optional)
    assistItemId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdLedgersAgeingGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdLedgersAgeingGet(body);
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
| **assistType** | `number` |  | [Optional] [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **accountFrom** | `string` |  | [Optional] [Defaults to `undefined`] |
| **accountTo** | `string` |  | [Optional] [Defaults to `undefined`] |
| **assistItemId** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**AgeingAnalysisDto**](AgeingAnalysisDto.md)

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


## accountsetsAccountSetIdLedgersAssistBalanceGet

> AssistBalanceDto accountsetsAccountSetIdLedgersAssistBalanceGet(accountSetId, assistType, startDate, endDate, accountFrom, accountTo, assistItemId, hideZeroBalance, hideNoActivity, includeIdleAccounts)



### Example

```ts
import {
  Configuration,
  LedgersApi,
} from '';
import type { AccountsetsAccountSetIdLedgersAssistBalanceGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new LedgersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    assistType: 56,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    accountFrom: accountFrom_example,
    // string (optional)
    accountTo: accountTo_example,
    // string (optional)
    assistItemId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // boolean (optional)
    hideZeroBalance: true,
    // boolean (optional)
    hideNoActivity: true,
    // boolean (optional)
    includeIdleAccounts: true,
  } satisfies AccountsetsAccountSetIdLedgersAssistBalanceGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdLedgersAssistBalanceGet(body);
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
| **assistType** | `number` |  | [Optional] [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **accountFrom** | `string` |  | [Optional] [Defaults to `undefined`] |
| **accountTo** | `string` |  | [Optional] [Defaults to `undefined`] |
| **assistItemId** | `string` |  | [Optional] [Defaults to `undefined`] |
| **hideZeroBalance** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **hideNoActivity** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **includeIdleAccounts** | `boolean` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**AssistBalanceDto**](AssistBalanceDto.md)

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


## accountsetsAccountSetIdLedgersAssistCombineGet

> AssistCombineDto accountsetsAccountSetIdLedgersAssistCombineGet(accountSetId, assistType, secondaryAssistType, startDate, endDate, accountFrom, accountTo)



### Example

```ts
import {
  Configuration,
  LedgersApi,
} from '';
import type { AccountsetsAccountSetIdLedgersAssistCombineGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new LedgersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    assistType: 56,
    // number (optional)
    secondaryAssistType: 56,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    accountFrom: accountFrom_example,
    // string (optional)
    accountTo: accountTo_example,
  } satisfies AccountsetsAccountSetIdLedgersAssistCombineGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdLedgersAssistCombineGet(body);
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
| **assistType** | `number` |  | [Optional] [Defaults to `undefined`] |
| **secondaryAssistType** | `number` |  | [Optional] [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **accountFrom** | `string` |  | [Optional] [Defaults to `undefined`] |
| **accountTo** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**AssistCombineDto**](AssistCombineDto.md)

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


## accountsetsAccountSetIdLedgersAssistSubsidiaryGet

> AssistSubsidiaryDto accountsetsAccountSetIdLedgersAssistSubsidiaryGet(accountSetId, assistType, startDate, endDate, accountFrom, accountTo, assistItemId, summaryKeyword, showCounterAccount, hideZeroBalance, hideNoActivity)



### Example

```ts
import {
  Configuration,
  LedgersApi,
} from '';
import type { AccountsetsAccountSetIdLedgersAssistSubsidiaryGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new LedgersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    assistType: 56,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    accountFrom: accountFrom_example,
    // string (optional)
    accountTo: accountTo_example,
    // string (optional)
    assistItemId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    summaryKeyword: summaryKeyword_example,
    // boolean (optional)
    showCounterAccount: true,
    // boolean (optional)
    hideZeroBalance: true,
    // boolean (optional)
    hideNoActivity: true,
  } satisfies AccountsetsAccountSetIdLedgersAssistSubsidiaryGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdLedgersAssistSubsidiaryGet(body);
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
| **assistType** | `number` |  | [Optional] [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **accountFrom** | `string` |  | [Optional] [Defaults to `undefined`] |
| **accountTo** | `string` |  | [Optional] [Defaults to `undefined`] |
| **assistItemId** | `string` |  | [Optional] [Defaults to `undefined`] |
| **summaryKeyword** | `string` |  | [Optional] [Defaults to `undefined`] |
| **showCounterAccount** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **hideZeroBalance** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **hideNoActivity** | `boolean` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**AssistSubsidiaryDto**](AssistSubsidiaryDto.md)

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


## accountsetsAccountSetIdLedgersGeneralGet

> GeneralLedgerDto accountsetsAccountSetIdLedgersGeneralGet(accountSetId, startDate, endDate, accountFrom, accountTo, minLevel, maxLevel, showAssist, showOnlyLeaf, hideZeroBalance, hideNoActivity, hideTotalsNoActivity, includeIdleAccounts)



### Example

```ts
import {
  Configuration,
  LedgersApi,
} from '';
import type { AccountsetsAccountSetIdLedgersGeneralGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new LedgersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    accountFrom: accountFrom_example,
    // string (optional)
    accountTo: accountTo_example,
    // number (optional)
    minLevel: 56,
    // number (optional)
    maxLevel: 56,
    // boolean (optional)
    showAssist: true,
    // boolean (optional)
    showOnlyLeaf: true,
    // boolean (optional)
    hideZeroBalance: true,
    // boolean (optional)
    hideNoActivity: true,
    // boolean (optional)
    hideTotalsNoActivity: true,
    // boolean (optional)
    includeIdleAccounts: true,
  } satisfies AccountsetsAccountSetIdLedgersGeneralGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdLedgersGeneralGet(body);
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
| **accountFrom** | `string` |  | [Optional] [Defaults to `undefined`] |
| **accountTo** | `string` |  | [Optional] [Defaults to `undefined`] |
| **minLevel** | `number` |  | [Optional] [Defaults to `undefined`] |
| **maxLevel** | `number` |  | [Optional] [Defaults to `undefined`] |
| **showAssist** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **showOnlyLeaf** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **hideZeroBalance** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **hideNoActivity** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **hideTotalsNoActivity** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **includeIdleAccounts** | `boolean` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**GeneralLedgerDto**](GeneralLedgerDto.md)

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


## accountsetsAccountSetIdLedgersJournalGet

> VoucherJournalDto accountsetsAccountSetIdLedgersJournalGet(accountSetId, startDate, endDate, accountFrom, accountTo, summaryKeyword, sortBy, showOnlyLeaf)



### Example

```ts
import {
  Configuration,
  LedgersApi,
} from '';
import type { AccountsetsAccountSetIdLedgersJournalGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new LedgersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    accountFrom: accountFrom_example,
    // string (optional)
    accountTo: accountTo_example,
    // string (optional)
    summaryKeyword: summaryKeyword_example,
    // number (optional)
    sortBy: 56,
    // boolean (optional)
    showOnlyLeaf: true,
  } satisfies AccountsetsAccountSetIdLedgersJournalGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdLedgersJournalGet(body);
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
| **accountFrom** | `string` |  | [Optional] [Defaults to `undefined`] |
| **accountTo** | `string` |  | [Optional] [Defaults to `undefined`] |
| **summaryKeyword** | `string` |  | [Optional] [Defaults to `undefined`] |
| **sortBy** | `number` |  | [Optional] [Defaults to `undefined`] |
| **showOnlyLeaf** | `boolean` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**VoucherJournalDto**](VoucherJournalDto.md)

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


## accountsetsAccountSetIdLedgersKindExportGet

> accountsetsAccountSetIdLedgersKindExportGet(accountSetId, kind, startDate, endDate, accountFrom, accountTo, maxLevel, minLevel, summaryKeyword, sortBy, showAssist, showCounterAccount, showOnlyLeaf, hideZeroBalance, hideNoActivity, hideTotalsNoActivity, includeIdleAccounts, assistType, secondaryAssistType, assistItemId, accountId)



### Example

```ts
import {
  Configuration,
  LedgersApi,
} from '';
import type { AccountsetsAccountSetIdLedgersKindExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new LedgersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    kind: kind_example,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    accountFrom: accountFrom_example,
    // string (optional)
    accountTo: accountTo_example,
    // number (optional)
    maxLevel: 56,
    // number (optional)
    minLevel: 56,
    // string (optional)
    summaryKeyword: summaryKeyword_example,
    // number (optional)
    sortBy: 56,
    // boolean (optional)
    showAssist: true,
    // boolean (optional)
    showCounterAccount: true,
    // boolean (optional)
    showOnlyLeaf: true,
    // boolean (optional)
    hideZeroBalance: true,
    // boolean (optional)
    hideNoActivity: true,
    // boolean (optional)
    hideTotalsNoActivity: true,
    // boolean (optional)
    includeIdleAccounts: true,
    // number (optional)
    assistType: 56,
    // number (optional)
    secondaryAssistType: 56,
    // string (optional)
    assistItemId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    accountId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdLedgersKindExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdLedgersKindExportGet(body);
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
| **kind** | `string` |  | [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **accountFrom** | `string` |  | [Optional] [Defaults to `undefined`] |
| **accountTo** | `string` |  | [Optional] [Defaults to `undefined`] |
| **maxLevel** | `number` |  | [Optional] [Defaults to `undefined`] |
| **minLevel** | `number` |  | [Optional] [Defaults to `undefined`] |
| **summaryKeyword** | `string` |  | [Optional] [Defaults to `undefined`] |
| **sortBy** | `number` |  | [Optional] [Defaults to `undefined`] |
| **showAssist** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **showCounterAccount** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **showOnlyLeaf** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **hideZeroBalance** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **hideNoActivity** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **hideTotalsNoActivity** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **includeIdleAccounts** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **assistType** | `number` |  | [Optional] [Defaults to `undefined`] |
| **secondaryAssistType** | `number` |  | [Optional] [Defaults to `undefined`] |
| **assistItemId** | `string` |  | [Optional] [Defaults to `undefined`] |
| **accountId** | `string` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdLedgersMultiColumnGet

> MultiColumnDto accountsetsAccountSetIdLedgersMultiColumnGet(accountSetId, accountId, startDate, endDate, summaryKeyword)



### Example

```ts
import {
  Configuration,
  LedgersApi,
} from '';
import type { AccountsetsAccountSetIdLedgersMultiColumnGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new LedgersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    accountId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    summaryKeyword: summaryKeyword_example,
  } satisfies AccountsetsAccountSetIdLedgersMultiColumnGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdLedgersMultiColumnGet(body);
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
| **accountId** | `string` |  | [Optional] [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **summaryKeyword** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**MultiColumnDto**](MultiColumnDto.md)

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


## accountsetsAccountSetIdLedgersSubsidiaryGet

> SubsidiaryLedgerDto accountsetsAccountSetIdLedgersSubsidiaryGet(accountSetId, startDate, endDate, accountFrom, accountTo, maxLevel, minLevel, summaryKeyword, sortBy, showAssist, showCounterAccount, showOnlyLeaf, hideZeroBalance, hideNoActivity, hideTotalsNoActivity, includeIdleAccounts)



### Example

```ts
import {
  Configuration,
  LedgersApi,
} from '';
import type { AccountsetsAccountSetIdLedgersSubsidiaryGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new LedgersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    accountFrom: accountFrom_example,
    // string (optional)
    accountTo: accountTo_example,
    // number (optional)
    maxLevel: 56,
    // number (optional)
    minLevel: 56,
    // string (optional)
    summaryKeyword: summaryKeyword_example,
    // number (optional)
    sortBy: 56,
    // boolean (optional)
    showAssist: true,
    // boolean (optional)
    showCounterAccount: true,
    // boolean (optional)
    showOnlyLeaf: true,
    // boolean (optional)
    hideZeroBalance: true,
    // boolean (optional)
    hideNoActivity: true,
    // boolean (optional)
    hideTotalsNoActivity: true,
    // boolean (optional)
    includeIdleAccounts: true,
  } satisfies AccountsetsAccountSetIdLedgersSubsidiaryGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdLedgersSubsidiaryGet(body);
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
| **accountFrom** | `string` |  | [Optional] [Defaults to `undefined`] |
| **accountTo** | `string` |  | [Optional] [Defaults to `undefined`] |
| **maxLevel** | `number` |  | [Optional] [Defaults to `undefined`] |
| **minLevel** | `number` |  | [Optional] [Defaults to `undefined`] |
| **summaryKeyword** | `string` |  | [Optional] [Defaults to `undefined`] |
| **sortBy** | `number` |  | [Optional] [Defaults to `undefined`] |
| **showAssist** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **showCounterAccount** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **showOnlyLeaf** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **hideZeroBalance** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **hideNoActivity** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **hideTotalsNoActivity** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **includeIdleAccounts** | `boolean` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**SubsidiaryLedgerDto**](SubsidiaryLedgerDto.md)

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


## accountsetsAccountSetIdLedgersVoucherSummaryGet

> VoucherSummaryDto accountsetsAccountSetIdLedgersVoucherSummaryGet(accountSetId, startDate, endDate, accountFrom, accountTo, summaryKeyword, showOnlyLeaf)



### Example

```ts
import {
  Configuration,
  LedgersApi,
} from '';
import type { AccountsetsAccountSetIdLedgersVoucherSummaryGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new LedgersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    accountFrom: accountFrom_example,
    // string (optional)
    accountTo: accountTo_example,
    // string (optional)
    summaryKeyword: summaryKeyword_example,
    // boolean (optional)
    showOnlyLeaf: true,
  } satisfies AccountsetsAccountSetIdLedgersVoucherSummaryGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdLedgersVoucherSummaryGet(body);
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
| **accountFrom** | `string` |  | [Optional] [Defaults to `undefined`] |
| **accountTo** | `string` |  | [Optional] [Defaults to `undefined`] |
| **summaryKeyword** | `string` |  | [Optional] [Defaults to `undefined`] |
| **showOnlyLeaf** | `boolean` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**VoucherSummaryDto**](VoucherSummaryDto.md)

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

