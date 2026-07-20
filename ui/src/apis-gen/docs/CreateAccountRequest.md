
# CreateAccountRequest


## Properties

Name | Type
------------ | -------------
`parentId` | string
`code` | string
`name` | string
`category` | number
`direction` | number

## Example

```typescript
import type { CreateAccountRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "parentId": null,
  "code": null,
  "name": null,
  "category": null,
  "direction": null,
} satisfies CreateAccountRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CreateAccountRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


