
# AccountDto


## Properties

Name | Type
------------ | -------------
`id` | string
`code` | string
`name` | string
`category` | number
`direction` | number
`parentId` | string
`level` | number
`isLeaf` | boolean
`isEnabled` | boolean
`mnemonic` | string

## Example

```typescript
import type { AccountDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "code": null,
  "name": null,
  "category": null,
  "direction": null,
  "parentId": null,
  "level": null,
  "isLeaf": null,
  "isEnabled": null,
  "mnemonic": null,
} satisfies AccountDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as AccountDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


