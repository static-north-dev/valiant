namespace Valiant.Mapping.Samples.FlatteningAndEnums;

[ValiantMapper]
public sealed partial class OfficeMapper : MapperDefinition<Office, OfficeDto>;

[ValiantMapper]
public sealed partial class OfficeImportMapper : MapperDefinition<OfficeImport, OfficeRecord>;
