
# BalanceSheetDto


## Properties

Name | Type
------------ | -------------
`standard` | number
`templateTitle` | string
`year` | number
`month` | number
`endingDate` | string
`openingDate` | string
`lines` | [Array&lt;BalanceSheetLineDto&gt;](BalanceSheetLineDto.md)

## Example

```typescript
import type { BalanceSheetDto } from ''

// TODO: Update the object below with actual values
const example = {
  "standard": null,
  "templateTitle": null,
  "year": null,
  "month": null,
  "endingDate": null,
  "openingDate": null,
  "lines": null,
} satisfies BalanceSheetDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as BalanceSheetDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


