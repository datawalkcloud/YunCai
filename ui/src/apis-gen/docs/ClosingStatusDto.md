
# ClosingStatusDto


## Properties

Name | Type
------------ | -------------
`startPeriod` | string
`carriedPeriod` | string
`closedPeriod` | string
`nextCarryYear` | number
`nextCarryMonth` | number
`nextCloseYear` | number
`nextCloseMonth` | number
`canReopen` | boolean
`canCancelCarry` | boolean
`periods` | [Array&lt;ClosingPeriodDto&gt;](ClosingPeriodDto.md)

## Example

```typescript
import type { ClosingStatusDto } from ''

// TODO: Update the object below with actual values
const example = {
  "startPeriod": null,
  "carriedPeriod": null,
  "closedPeriod": null,
  "nextCarryYear": null,
  "nextCarryMonth": null,
  "nextCloseYear": null,
  "nextCloseMonth": null,
  "canReopen": null,
  "canCancelCarry": null,
  "periods": null,
} satisfies ClosingStatusDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ClosingStatusDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


