
# TaxPortalBrowserCookieDto


## Properties

Name | Type
------------ | -------------
`name` | string
`value` | string
`domain` | string
`path` | string
`expires` | number
`httpOnly` | boolean
`secure` | boolean
`sameSite` | string

## Example

```typescript
import type { TaxPortalBrowserCookieDto } from ''

// TODO: Update the object below with actual values
const example = {
  "name": null,
  "value": null,
  "domain": null,
  "path": null,
  "expires": null,
  "httpOnly": null,
  "secure": null,
  "sameSite": null,
} satisfies TaxPortalBrowserCookieDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxPortalBrowserCookieDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


