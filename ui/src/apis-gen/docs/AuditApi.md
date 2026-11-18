# AuditApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdAuditArchiveExcelGet**](AuditApi.md#accountsetsaccountsetidauditarchiveexcelget) | **GET** /accountsets/{accountSetId}/audit/archive/excel |  |
| [**accountsetsAccountSetIdAuditArchivePdfGet**](AuditApi.md#accountsetsaccountsetidauditarchivepdfget) | **GET** /accountsets/{accountSetId}/audit/archive/pdf |  |
| [**accountsetsAccountSetIdAuditExtractGet**](AuditApi.md#accountsetsaccountsetidauditextractget) | **GET** /accountsets/{accountSetId}/audit/extract |  |



## accountsetsAccountSetIdAuditArchiveExcelGet

> accountsetsAccountSetIdAuditArchiveExcelGet(accountSetId)



### Example

```ts
import {
  Configuration,
  AuditApi,
} from '';
import type { AccountsetsAccountSetIdAuditArchiveExcelGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AuditApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdAuditArchiveExcelGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdAuditArchiveExcelGet(body);
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


## accountsetsAccountSetIdAuditArchivePdfGet

> accountsetsAccountSetIdAuditArchivePdfGet(accountSetId)



### Example

```ts
import {
  Configuration,
  AuditApi,
} from '';
import type { AccountsetsAccountSetIdAuditArchivePdfGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AuditApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdAuditArchivePdfGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdAuditArchivePdfGet(body);
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


## accountsetsAccountSetIdAuditExtractGet

> accountsetsAccountSetIdAuditExtractGet(accountSetId, year, month)



### Example

```ts
import {
  Configuration,
  AuditApi,
} from '';
import type { AccountsetsAccountSetIdAuditExtractGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AuditApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
  } satisfies AccountsetsAccountSetIdAuditExtractGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdAuditExtractGet(body);
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

