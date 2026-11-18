
# SendSmsCodeResult


## Properties

Name | Type
------------ | -------------
`success` | boolean
`retrySeconds` | number
`message` | string
`debugCode` | string

## Example

```typescript
import type { SendSmsCodeResult } from ''

// TODO: Update the object below with actual values
const example = {
  "success": null,
  "retrySeconds": null,
  "message": null,
  "debugCode": null,
} satisfies SendSmsCodeResult

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SendSmsCodeResult
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


