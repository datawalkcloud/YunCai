# AccountsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdAccountsExportGet**](AccountsApi.md#accountsetsaccountsetidaccountsexportget) | **GET** /accountsets/{accountSetId}/Accounts/export |  |
| [**accountsetsAccountSetIdAccountsGet**](AccountsApi.md#accountsetsaccountsetidaccountsget) | **GET** /accountsets/{accountSetId}/Accounts |  |
| [**accountsetsAccountSetIdAccountsIdAssistTypesGet**](AccountsApi.md#accountsetsaccountsetidaccountsidassisttypesget) | **GET** /accountsets/{accountSetId}/Accounts/{id}/assist-types |  |
| [**accountsetsAccountSetIdAccountsIdAssistTypesPut**](AccountsApi.md#accountsetsaccountsetidaccountsidassisttypesput) | **PUT** /accountsets/{accountSetId}/Accounts/{id}/assist-types |  |
| [**accountsetsAccountSetIdAccountsIdDelete**](AccountsApi.md#accountsetsaccountsetidaccountsiddelete) | **DELETE** /accountsets/{accountSetId}/Accounts/{id} |  |
| [**accountsetsAccountSetIdAccountsIdGet**](AccountsApi.md#accountsetsaccountsetidaccountsidget) | **GET** /accountsets/{accountSetId}/Accounts/{id} |  |
| [**accountsetsAccountSetIdAccountsIdPut**](AccountsApi.md#accountsetsaccountsetidaccountsidput) | **PUT** /accountsets/{accountSetId}/Accounts/{id} |  |
| [**accountsetsAccountSetIdAccountsImportPost**](AccountsApi.md#accountsetsaccountsetidaccountsimportpost) | **POST** /accountsets/{accountSetId}/Accounts/import |  |
| [**accountsetsAccountSetIdAccountsImportTemplateGet**](AccountsApi.md#accountsetsaccountsetidaccountsimporttemplateget) | **GET** /accountsets/{accountSetId}/Accounts/import/template |  |
| [**accountsetsAccountSetIdAccountsOpeningBalancesGet**](AccountsApi.md#accountsetsaccountsetidaccountsopeningbalancesget) | **GET** /accountsets/{accountSetId}/Accounts/opening-balances |  |
| [**accountsetsAccountSetIdAccountsOpeningBalancesPut**](AccountsApi.md#accountsetsaccountsetidaccountsopeningbalancesput) | **PUT** /accountsets/{accountSetId}/Accounts/opening-balances |  |
| [**accountsetsAccountSetIdAccountsPost**](AccountsApi.md#accountsetsaccountsetidaccountspost) | **POST** /accountsets/{accountSetId}/Accounts |  |



## accountsetsAccountSetIdAccountsExportGet

> accountsetsAccountSetIdAccountsExportGet(accountSetId)



### Example

```ts
import {
  Configuration,
  AccountsApi,
} from '';
import type { AccountsetsAccountSetIdAccountsExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdAccountsExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdAccountsExportGet(body);
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


## accountsetsAccountSetIdAccountsGet

> Array&lt;AccountDto&gt; accountsetsAccountSetIdAccountsGet(accountSetId, leafOnly)



### Example

```ts
import {
  Configuration,
  AccountsApi,
} from '';
import type { AccountsetsAccountSetIdAccountsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // boolean (optional)
    leafOnly: true,
  } satisfies AccountsetsAccountSetIdAccountsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdAccountsGet(body);
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
| **leafOnly** | `boolean` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**Array&lt;AccountDto&gt;**](AccountDto.md)

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


## accountsetsAccountSetIdAccountsIdAssistTypesGet

> Array&lt;AccountAssistTypeDto&gt; accountsetsAccountSetIdAccountsIdAssistTypesGet(accountSetId, id)



### Example

```ts
import {
  Configuration,
  AccountsApi,
} from '';
import type { AccountsetsAccountSetIdAccountsIdAssistTypesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdAccountsIdAssistTypesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdAccountsIdAssistTypesGet(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |

### Return type

[**Array&lt;AccountAssistTypeDto&gt;**](AccountAssistTypeDto.md)

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


## accountsetsAccountSetIdAccountsIdAssistTypesPut

> Array&lt;AccountAssistTypeDto&gt; accountsetsAccountSetIdAccountsIdAssistTypesPut(accountSetId, id, accountAssistTypeRequest)



### Example

```ts
import {
  Configuration,
  AccountsApi,
} from '';
import type { AccountsetsAccountSetIdAccountsIdAssistTypesPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Array<AccountAssistTypeRequest>
    accountAssistTypeRequest: ...,
  } satisfies AccountsetsAccountSetIdAccountsIdAssistTypesPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdAccountsIdAssistTypesPut(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |
| **accountAssistTypeRequest** | `Array<AccountAssistTypeRequest>` |  | |

### Return type

[**Array&lt;AccountAssistTypeDto&gt;**](AccountAssistTypeDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdAccountsIdDelete

> accountsetsAccountSetIdAccountsIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  AccountsApi,
} from '';
import type { AccountsetsAccountSetIdAccountsIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdAccountsIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdAccountsIdDelete(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |

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


## accountsetsAccountSetIdAccountsIdGet

> AccountDto accountsetsAccountSetIdAccountsIdGet(accountSetId, id)



### Example

```ts
import {
  Configuration,
  AccountsApi,
} from '';
import type { AccountsetsAccountSetIdAccountsIdGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdAccountsIdGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdAccountsIdGet(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |

### Return type

[**AccountDto**](AccountDto.md)

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


## accountsetsAccountSetIdAccountsIdPut

> AccountDto accountsetsAccountSetIdAccountsIdPut(accountSetId, id, updateAccountRequest)



### Example

```ts
import {
  Configuration,
  AccountsApi,
} from '';
import type { AccountsetsAccountSetIdAccountsIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // UpdateAccountRequest
    updateAccountRequest: ...,
  } satisfies AccountsetsAccountSetIdAccountsIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdAccountsIdPut(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |
| **updateAccountRequest** | [UpdateAccountRequest](UpdateAccountRequest.md) |  | |

### Return type

[**AccountDto**](AccountDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdAccountsImportPost

> AccountImportResult accountsetsAccountSetIdAccountsImportPost(accountSetId, file)



### Example

```ts
import {
  Configuration,
  AccountsApi,
} from '';
import type { AccountsetsAccountSetIdAccountsImportPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdAccountsImportPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdAccountsImportPost(body);
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
| **file** | `Blob` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**AccountImportResult**](AccountImportResult.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `multipart/form-data`
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdAccountsImportTemplateGet

> accountsetsAccountSetIdAccountsImportTemplateGet(accountSetId)



### Example

```ts
import {
  Configuration,
  AccountsApi,
} from '';
import type { AccountsetsAccountSetIdAccountsImportTemplateGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdAccountsImportTemplateGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdAccountsImportTemplateGet(body);
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


## accountsetsAccountSetIdAccountsOpeningBalancesGet

> OpeningBalancesDto accountsetsAccountSetIdAccountsOpeningBalancesGet(accountSetId)



### Example

```ts
import {
  Configuration,
  AccountsApi,
} from '';
import type { AccountsetsAccountSetIdAccountsOpeningBalancesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdAccountsOpeningBalancesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdAccountsOpeningBalancesGet(body);
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

[**OpeningBalancesDto**](OpeningBalancesDto.md)

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


## accountsetsAccountSetIdAccountsOpeningBalancesPut

> OpeningBalancesDto accountsetsAccountSetIdAccountsOpeningBalancesPut(accountSetId, saveOpeningBalancesRequest)



### Example

```ts
import {
  Configuration,
  AccountsApi,
} from '';
import type { AccountsetsAccountSetIdAccountsOpeningBalancesPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveOpeningBalancesRequest
    saveOpeningBalancesRequest: ...,
  } satisfies AccountsetsAccountSetIdAccountsOpeningBalancesPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdAccountsOpeningBalancesPut(body);
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
| **saveOpeningBalancesRequest** | [SaveOpeningBalancesRequest](SaveOpeningBalancesRequest.md) |  | |

### Return type

[**OpeningBalancesDto**](OpeningBalancesDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdAccountsPost

> AccountDto accountsetsAccountSetIdAccountsPost(accountSetId, createAccountRequest)



### Example

```ts
import {
  Configuration,
  AccountsApi,
} from '';
import type { AccountsetsAccountSetIdAccountsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // CreateAccountRequest
    createAccountRequest: ...,
  } satisfies AccountsetsAccountSetIdAccountsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdAccountsPost(body);
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
| **createAccountRequest** | [CreateAccountRequest](CreateAccountRequest.md) |  | |

### Return type

[**AccountDto**](AccountDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)

