# RolesApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdRolesCatalogGet**](RolesApi.md#accountsetsaccountsetidrolescatalogget) | **GET** /accountsets/{accountSetId}/roles/catalog |  |
| [**accountsetsAccountSetIdRolesGet**](RolesApi.md#accountsetsaccountsetidrolesget) | **GET** /accountsets/{accountSetId}/roles |  |
| [**accountsetsAccountSetIdRolesIdDelete**](RolesApi.md#accountsetsaccountsetidrolesiddelete) | **DELETE** /accountsets/{accountSetId}/roles/{id} |  |
| [**accountsetsAccountSetIdRolesIdPut**](RolesApi.md#accountsetsaccountsetidrolesidput) | **PUT** /accountsets/{accountSetId}/roles/{id} |  |
| [**accountsetsAccountSetIdRolesPost**](RolesApi.md#accountsetsaccountsetidrolespost) | **POST** /accountsets/{accountSetId}/roles |  |



## accountsetsAccountSetIdRolesCatalogGet

> Array&lt;PermissionModuleDto&gt; accountsetsAccountSetIdRolesCatalogGet(accountSetId)



### Example

```ts
import {
  Configuration,
  RolesApi,
} from '';
import type { AccountsetsAccountSetIdRolesCatalogGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new RolesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdRolesCatalogGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdRolesCatalogGet(body);
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

[**Array&lt;PermissionModuleDto&gt;**](PermissionModuleDto.md)

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


## accountsetsAccountSetIdRolesGet

> Array&lt;AccountSetRoleDto&gt; accountsetsAccountSetIdRolesGet(accountSetId)



### Example

```ts
import {
  Configuration,
  RolesApi,
} from '';
import type { AccountsetsAccountSetIdRolesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new RolesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdRolesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdRolesGet(body);
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

[**Array&lt;AccountSetRoleDto&gt;**](AccountSetRoleDto.md)

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


## accountsetsAccountSetIdRolesIdDelete

> accountsetsAccountSetIdRolesIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  RolesApi,
} from '';
import type { AccountsetsAccountSetIdRolesIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new RolesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdRolesIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdRolesIdDelete(body);
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


## accountsetsAccountSetIdRolesIdPut

> AccountSetRoleDto accountsetsAccountSetIdRolesIdPut(accountSetId, id, saveAccountSetRoleRequest)



### Example

```ts
import {
  Configuration,
  RolesApi,
} from '';
import type { AccountsetsAccountSetIdRolesIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new RolesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveAccountSetRoleRequest
    saveAccountSetRoleRequest: ...,
  } satisfies AccountsetsAccountSetIdRolesIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdRolesIdPut(body);
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
| **saveAccountSetRoleRequest** | [SaveAccountSetRoleRequest](SaveAccountSetRoleRequest.md) |  | |

### Return type

[**AccountSetRoleDto**](AccountSetRoleDto.md)

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


## accountsetsAccountSetIdRolesPost

> AccountSetRoleDto accountsetsAccountSetIdRolesPost(accountSetId, saveAccountSetRoleRequest)



### Example

```ts
import {
  Configuration,
  RolesApi,
} from '';
import type { AccountsetsAccountSetIdRolesPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new RolesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveAccountSetRoleRequest
    saveAccountSetRoleRequest: ...,
  } satisfies AccountsetsAccountSetIdRolesPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdRolesPost(body);
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
| **saveAccountSetRoleRequest** | [SaveAccountSetRoleRequest](SaveAccountSetRoleRequest.md) |  | |

### Return type

[**AccountSetRoleDto**](AccountSetRoleDto.md)

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

