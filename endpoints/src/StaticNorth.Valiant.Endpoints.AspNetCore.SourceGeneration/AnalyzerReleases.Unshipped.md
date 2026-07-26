### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
VLE001 | StaticNorth.Valiant.Endpoints | Error | Endpoint declaration must implement IEndpoint.
VLE002 | StaticNorth.Valiant.Endpoints | Error | Endpoint declaration must not implement both IEndpoint and IEndpointGroup.
VLE003 | StaticNorth.Valiant.Endpoints | Error | Endpoint group declarations must use ValiantEndpointGroupAttribute instead of ValiantEndpointAttribute.
VLE004 | StaticNorth.Valiant.Endpoints | Error | Endpoint parent group type must implement IEndpointGroup.
VLE005 | StaticNorth.Valiant.Endpoints | Error | Endpoint group declaration must implement IEndpointGroup.
VLE006 | StaticNorth.Valiant.Endpoints | Error | Endpoint declaration must be marked with ValiantEndpointAttribute.
VLE007 | StaticNorth.Valiant.Endpoints | Error | Endpoint group declaration must be marked with ValiantEndpointGroupAttribute.
VLE008 | StaticNorth.Valiant.Endpoints | Error | Endpoint declaration must use ValiantEndpointAttribute instead of ValiantEndpointGroupAttribute.
VLE009 | StaticNorth.Valiant.Endpoints | Error | Endpoint declaration must use only one ValiantEndpointAttribute form.
VLE010 | StaticNorth.Valiant.Endpoints | Error | Query object types discovered from Valiant endpoint request models must support generated partial query binding.
VLE011 | StaticNorth.Valiant.Endpoints | Error | Direct query parameters using the same generated parser type must use one JsonValues policy.
