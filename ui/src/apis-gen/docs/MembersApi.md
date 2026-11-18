# MembersApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdMembersGet**](MembersApi.md#accountsetsaccountsetidmembersget) | **GET** /accountsets/{accountSetId}/members |  |
| [**accountsetsAccountSetIdMembersIdDelete**](MembersApi.md#accountsetsaccountsetidmembersiddelete) | **DELETE** /accountsets/{accountSetId}/members/{id} |  |
| [**accountsetsAccountSetIdMembersIdEnabledPut**](MembersApi.md#accountsetsaccountsetidmembersidenabledput) | **PUT** /accountsets/{accountSetId}/members/{id}/enabled |  |
| [**accountsetsAccountSetIdMembersIdPut**](MembersApi.md#accountsetsaccountsetidmembersidput) | **PUT** /accountsets/{accountSetId}/members/{id} |  |
| [**accountsetsAccountSetIdMembersPost**](MembersApi.md#accountsetsaccountsetidmemberspost) | **POST** /accountsets/{accountSetId}/members |  |



## accountsetsAccountSetIdMembersGet

> Array&lt;AccountSetMemberDto&gt; accountsetsAccountSetIdMembersGet(accountSetId)



### Example

```ts
import {
  Configuration,
  MembersApi,
} from '';
import type { AccountsetsAccountSetIdMembersGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new MembersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdMembersGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdMembersGet(body);
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

[**Array&lt;AccountSetMemberDto&gt;**](AccountSetMemberDto.md)

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


## accountsetsAccountSetIdMembersIdDelete

> accountsetsAccountSetIdMembersIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  MembersApi,
} from '';
import type { AccountsetsAccountSetIdMembersIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new MembersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdMembersIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdMembersIdDelete(body);
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


## accountsetsAccountSetIdMembersIdEnabledPut

> AccountSetMemberDto accountsetsAccountSetIdMembersIdEnabledPut(accountSetId, id, toggleMemberRequest)



### Example

```ts
import {
  Configuration,
  MembersApi,
} from '';
import type { AccountsetsAccountSetIdMembersIdEnabledPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new MembersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ToggleMemberRequest
    toggleMemberRequest: ...,
  } satisfies AccountsetsAccountSetIdMembersIdEnabledPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdMembersIdEnabledPut(body);
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
| **toggleMemberRequest** | [ToggleMemberRequest](ToggleMemberRequest.md) |  | |

### Return type

[**AccountSetMemberDto**](AccountSetMemberDto.md)

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


## accountsetsAccountSetIdMembersIdPut

> AccountSetMemberDto accountsetsAccountSetIdMembersIdPut(accountSetId, id, saveAccountSetMemberRequest)



### Example

```ts
import {
  Configuration,
  MembersApi,
} from '';
import type { AccountsetsAccountSetIdMembersIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new MembersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveAccountSetMemberRequest
    saveAccountSetMemberRequest: ...,
  } satisfies AccountsetsAccountSetIdMembersIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdMembersIdPut(body);
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
| **saveAccountSetMemberRequest** | [SaveAccountSetMemberRequest](SaveAccountSetMemberRequest.md) |  | |

### Return type

[**AccountSetMemberDto**](AccountSetMemberDto.md)

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


## accountsetsAccountSetIdMembersPost

> AccountSetMemberDto accountsetsAccountSetIdMembersPost(accountSetId, saveAccountSetMemberRequest)



### Example

```ts
import {
  Configuration,
  MembersApi,
} from '';
import type { AccountsetsAccountSetIdMembersPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new MembersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveAccountSetMemberRequest
    saveAccountSetMemberRequest: ...,
  } satisfies AccountsetsAccountSetIdMembersPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdMembersPost(body);
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
| **saveAccountSetMemberRequest** | [SaveAccountSetMemberRequest](SaveAccountSetMemberRequest.md) |  | |

### Return type

[**AccountSetMemberDto**](AccountSetMemberDto.md)

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

