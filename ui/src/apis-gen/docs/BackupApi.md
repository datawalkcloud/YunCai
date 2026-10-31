# BackupApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdBackupGet**](BackupApi.md#accountsetsaccountsetidbackupget) | **GET** /accountsets/{accountSetId}/backup |  |
| [**accountsetsAccountSetIdBackupRestorePost**](BackupApi.md#accountsetsaccountsetidbackuprestorepost) | **POST** /accountsets/{accountSetId}/backup/restore |  |



## accountsetsAccountSetIdBackupGet

> accountsetsAccountSetIdBackupGet(accountSetId)



### Example

```ts
import {
  Configuration,
  BackupApi,
} from '';
import type { AccountsetsAccountSetIdBackupGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BackupApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdBackupGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdBackupGet(body);
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


## accountsetsAccountSetIdBackupRestorePost

> BackupRestoreResultDto accountsetsAccountSetIdBackupRestorePost(accountSetId, file)



### Example

```ts
import {
  Configuration,
  BackupApi,
} from '';
import type { AccountsetsAccountSetIdBackupRestorePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BackupApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdBackupRestorePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdBackupRestorePost(body);
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

[**BackupRestoreResultDto**](BackupRestoreResultDto.md)

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

