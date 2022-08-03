using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Moq;
using Xunit;
using Simple.OData.Client.V4.Adapter;

namespace Simple.OData.Client.Tests.AdapterV4
{
	public class MetadataTestsv4
	{
		[Fact]
		public void GetPropertyNames_Model_With_One_EntitySet_ReturnsCorrect_Properties()
		{
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			var edmProperty = CreateProperty("Version", EdmTypeKind.Primitive);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredKey).Returns(new[] { edmStrcuturalProperty.Object });
			entityType.Setup(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object, edmProperty.Object });
			
			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.Setup(x => x.Definition).Returns(entityType.Object);
			var edmType = new Mock<IEdmCollectionType>();
			edmType.Setup(x => x.ElementType).Returns(edmTypeReference.Object);

			var entitySet = CreateEntitySet("Materials");
			entitySet.Setup(x => x.Type).Returns(edmType.Object);
			var entityContainer = CreateEntityContainer(entitySet.Object);
			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityContainer.Object, entityType.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			var properties = metadata.GetPropertyNames("Materials").ToList();
			Assert.Same("Version", properties.First());
			Assert.Equal(1, properties.Count);
		}

		[Fact]
		public void GetPropertyNames_Model_With_One_EntityType_With_NavigationProperty_ReturnsCorrect_Properties()
		{
			var navigationEntityType = CreateEntityType("Description");
			var edmnavigationProperty = CreateNavigationProperty("Description");
			edmnavigationProperty.Setup(x => x.DeclaringType).Returns(navigationEntityType.Object);

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			var edmProperty = CreateProperty("Version", EdmTypeKind.Primitive);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredKey).Returns(new[] { edmStrcuturalProperty.Object });
			entityType.Setup(x => x.DeclaredProperties).Returns(new List<IEdmProperty> { edmStrcuturalProperty.Object, edmProperty.Object, edmnavigationProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			var properties = metadata.GetPropertyNames("Material").ToList();
			Assert.Same("Version", properties.First());
			Assert.Equal(1, properties.Count);
		}
		[Fact]
		public void GetEntitySetNames_Model_With_One_EntitySet_Returns_One_EntitySetName()
		{

			const string expectedEntitySetName = "Materials";
			var entitySet = CreateEntitySet(expectedEntitySetName);
			var entityContainer = CreateEntityContainer(entitySet.Object);
			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityContainer.Object });

			var metadata = new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			var entitySetNames = metadata.GetEntitySetNames().ToList();
			Assert.Same(expectedEntitySetName, entitySetNames.First());
			Assert.Equal(1, entitySetNames.Count);
		}

		[Fact]
		public void GetEntitySetNames_Model_With_No_EntitySet_Returns_An_Empty_List()
		{

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement>());

			Assert.Empty(new Metadata(edmModel.Object, new BestMatchResolver(), false, false).GetEntitySetNames());
		}

		[Fact]
		public void GetEntityTypeNames_Model_With_Two_EntityTypes_Returns_Two_EntityTypeNames()
		{

			const string expectedEntityTypeName = "Material";
			var entityType = CreateEntityType(expectedEntityTypeName);
			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });
			
			var metadata = new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			var entityTypeNames = metadata.GetEntityTypeNames().ToList();
			Assert.Same(expectedEntityTypeName, entityTypeNames.First());
			Assert.Equal(1, entityTypeNames.Count);
		}

		[Fact]
		public void GetEntityTypeNames_Model_With_No_EntityType_Returns_An_Empty_List()
		{

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement>());
			Assert.Equal(0, new Metadata(edmModel.Object, new BestMatchResolver(), false, false).GetEntityTypeNames().Count());
		}

		[Fact]
		public void GetNavigationPropertyNames_Returns_On_A_Model_With_Two_EntityTypes_For_EntitySetName_One_NavigationProperty()
		{
			var navigationEntityType = CreateEntityType("Description");

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			var edmnavigationProperty = CreateNavigationProperty("Description");
			edmnavigationProperty.Setup(x => x.DeclaringType).Returns(navigationEntityType.Object);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredKey).Returns(new[] { edmStrcuturalProperty.Object });
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmnavigationProperty.Object });


			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.Setup(x => x.Definition).Returns(entityType.Object);
			var edmType = new Mock<IEdmCollectionType>();
			edmType.Setup(x => x.ElementType).Returns(edmTypeReference.Object);

			var entitySet = CreateEntitySet("Materials");
			entitySet.Setup(x => x.Type).Returns(edmType.Object);
			var entityContainer = CreateEntityContainer(entitySet.Object);
			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityContainer.Object, entityType.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			var navigationPropertyNames = metadata.GetNavigationPropertyNames("Materials").ToList();
			Assert.Equal(1, navigationPropertyNames.Count);
			Assert.Equal("Description", navigationPropertyNames.First());
		}

		[Fact]
		public void GetNavigationPropertyNames_Returns_On_A_Model_With_Two_EntityTypes_For_EntityTypeName_One_NavigationProperty()
		{
			var navigationEntityType = CreateEntityType("Description");

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			var edmnavigationProperty = CreateNavigationProperty("Description");
			edmnavigationProperty.Setup(x => x.DeclaringType).Returns(navigationEntityType.Object);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredKey).Returns(new[] { edmStrcuturalProperty.Object });
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmnavigationProperty.Object });
			
			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.Setup(x => x.Definition).Returns(entityType.Object);
			var edmType = new Mock<IEdmCollectionType>();
			edmType.Setup(x => x.ElementType).Returns(edmTypeReference.Object);

			var entitySet = CreateEntitySet("Materials");
			entitySet.Setup(x => x.Type).Returns(edmType.Object);
			var entityContainer = CreateEntityContainer(entitySet.Object);
			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityContainer.Object, entityType.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			var navigationPropertyNames = metadata.GetNavigationPropertyNames("Material").ToList();
			Assert.Equal(1, navigationPropertyNames.Count);
			Assert.Equal("Description", navigationPropertyNames.First());
		}

		[Fact]
		public void GetNavigationPropertyNames_Model_With_No_NavigationProperties_Returns_An_Empty_List()
		{
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredKey).Returns(new[] { edmStrcuturalProperty.Object });

			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.Setup(x => x.Definition).Returns(entityType.Object);
			var edmType = new Mock<IEdmCollectionType>();
			edmType.Setup(x => x.ElementType).Returns(edmTypeReference.Object);

			var entitySet = CreateEntitySet("Materials");
			entitySet.Setup(x => x.Type).Returns(edmType.Object);
			var entityContainer = CreateEntityContainer(entitySet.Object);
			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityContainer.Object, entityType.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Empty(metadata.GetNavigationPropertyNames("Materials"));
		}

		[Fact]
		public void GetNavigationPropertyNames_Model_With_Wrong_CollectionName_Throws_Exception()
		{
			var entitySet = CreateEntitySet("Materials");
			var entityContainer = CreateEntityContainer(entitySet.Object);
			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityContainer.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(()=>metadata.GetNavigationPropertyNames("31"));
		}

		[Fact]
		public void GetNavigationPropertyNames_Model_With_One_EntitySet_One_NormalProperty_One_NavigationProperty_Returns_One_NavigationProperty()
		{
			var navigationEntityType = CreateEntityType("Description");

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			var edmNormalProperty = CreateProperty("Version", EdmTypeKind.Primitive);
			var edmnavigationProperty = CreateNavigationProperty("Description");
			edmnavigationProperty.Setup(x => x.DeclaringType).Returns(navigationEntityType.Object);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredKey).Returns(new[] { edmStrcuturalProperty.Object });
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new List<IEdmProperty> { edmNormalProperty.Object, edmnavigationProperty.Object });

			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.Setup(x => x.Definition).Returns(entityType.Object);
			var edmType = new Mock<IEdmCollectionType>();
			edmType.Setup(x => x.ElementType).Returns(edmTypeReference.Object);

			var entitySet = CreateEntitySet("Materials");
			entitySet.SetupGet(x => x.Type).Returns(edmType.Object);
			var entityContainer = CreateEntityContainer(entitySet.Object);
			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityContainer.Object, entityType.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			var navigationPropertyNames = metadata.GetNavigationPropertyNames("Materials").ToList();
			Assert.Equal(1, navigationPropertyNames.Count);
			Assert.Equal("Description", navigationPropertyNames.First());
		}


		[Fact]
		public void GetPropertyType_Model_With_Two_EntityTypes_Returns_Correct_Type_For_KeyProperty_Of_Passed_EntityTypeName()
		{
			var edmStrcuturalProperty = CreateProperty("Id", EdmTypeKind.Primitive, EdmPrimitiveTypeKind.Int32);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });
			entityType.Setup(x => x.FindProperty("Id")).Returns(edmStrcuturalProperty.Object);

			var entityType2 = CreateEntityType("Description");
			var edmStrcuturalProperty2 = CreateProperty("Id", EdmTypeKind.Primitive);
			entityType2.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty2.Object });
			entityType2.Setup(x => x.FindProperty("Id")).Returns(edmStrcuturalProperty2.Object);

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object, entityType2.Object });

			var metadata = new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Same(typeof(int), metadata.GetPropertyType("Material", "Id"));
		}

		[Fact]
		public void GetPropertyType_Model_With_One_EntityType_Returns_Correct_Type_For_NormalProperty_Of_Passed_EntityTypeName()
		{
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPrimitiveTypeKind.Boolean);
			
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });
			entityType.Setup(x => x.FindProperty("Number")).Returns(edmStrcuturalProperty.Object);

			var entityType2 = CreateEntityType("Description");
			var edmStrcuturalProperty2 = CreateProperty("Number", EdmTypeKind.Primitive);
			entityType2.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty2.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object, entityType2.Object });

			var metadata = new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Same(typeof(bool), metadata.GetPropertyType("Material", "Number"));
		}

		[Fact]
		public void GetPropertyType_Model_With_Invalid_CollectionName_Throws_Exception()
		{
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredKey).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });


			var metadata = new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetPropertyType("Invalid", "Number"));
		}

		[Fact]
		public void GetPropertyType_Model_With_Invalid_PropertyName_Throws_Exception()
		{
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredKey).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });


			var metadata = new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetPropertyType("Material", "Invalid"));
		}

		[Fact]
		public void GetPropertyType_Model_With_One_EntitySet_Returns_List_Of_objects_Type_For_NavigationProperty_Which_IsCollection_Of_Passed_EntitySetName()
		{
			var expectedType = typeof(IEnumerable<object>);
			var edmStrcuturalProperty = CreateNavigationProperty("Descriptions", EdmTypeKind.Collection);

			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });
			entityType.Setup(x => x.FindProperty("Descriptions")).Returns(edmStrcuturalProperty.Object);

			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.Setup(x => x.Definition).Returns(entityType.Object);
			var edmType = new Mock<IEdmCollectionType>();
			edmType.Setup(x => x.ElementType).Returns(edmTypeReference.Object);

			var entitySet = CreateEntitySet("Materials");
			entitySet.SetupGet(x => x.Type).Returns(edmType.Object);

			var entityContainer = CreateEntityContainer(entitySet.Object);

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object, entityContainer.Object });

			var metadata = new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Same(expectedType, metadata.GetPropertyType("Materials", "Descriptions"));
		}

		[Fact]
		public void GetPropertyDefaultValue_With_EntitySetName_And_Valid_Property_Returns_Correct_String()
		{
			const string expectedDefaultValue = "31";
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			edmStrcuturalProperty.SetupGet(x => x.DefaultValueString).Returns(expectedDefaultValue);

			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });
			entityType.Setup(x => x.FindProperty("Number")).Returns(edmStrcuturalProperty.Object);

			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.Setup(x => x.Definition).Returns(entityType.Object);
			var edmType = new Mock<IEdmCollectionType>();
			edmType.Setup(x => x.ElementType).Returns(edmTypeReference.Object);

			var entitySet = CreateEntitySet("Materials");
			entitySet.SetupGet(x => x.Type).Returns(edmType.Object);
			var entityContainer = CreateEntityContainer(entitySet.Object);

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object, entityContainer.Object });
			
			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Same(expectedDefaultValue, metadata.GetPropertyDefaultValue("Materials", "Number"));
		}

		[Fact]
		public void GetPropertyDefaultValue_With_EntityTypeName_And_Valid_Property_Returns_Correct_String()
		{
			const string expectedDefaultValue = "69";
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			edmStrcuturalProperty.SetupGet(x => x.DefaultValueString).Returns(expectedDefaultValue);

			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });
			entityType.Setup(x => x.FindProperty("Number")).Returns(edmStrcuturalProperty.Object);

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Same(expectedDefaultValue, metadata.GetPropertyDefaultValue("Material", "Number"));
		}

		[Fact]
		public void GetPropertyDefaultValue_With_EntityTypeName_And_InValid_Property_Throws_Exception()
		{
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			edmStrcuturalProperty.SetupGet(x => x.DefaultValueString).Returns("31");
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetPropertyDefaultValue("Material", "Exception"));
		}

		[Fact]
		public void GetPropertyDefaultValue_With_EntitySetName_And_InValid_Property_Throws_Exception()
		{
			var entityType = CreateEntityType("Material");
			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.Setup(x => x.Definition).Returns(entityType.Object);
			var edmType = new Mock<IEdmCollectionType>();
			edmType.Setup(x => x.ElementType).Returns(edmTypeReference.Object);

			var entitySet = CreateEntitySet("Materials");
			entitySet.SetupGet(x => x.Type).Returns(edmType.Object);
			var entityContainer = CreateEntityContainer(entitySet.Object);

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityContainer.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetPropertyDefaultValue("Material", "Ehy"));
		}

		[Fact]
		public void GetPropertyDefaultValue_With_Invalid_CollectionName_And_Valid_Property_Throws_Exception()
		{
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			edmStrcuturalProperty.SetupGet(x => x.DefaultValueString).Returns("31");
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredKey).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetPropertyDefaultValue("31", "Number"));
		}

		[Fact]
		public void PropertyIsNullable_With_EntitySetName_And_Valid_Property_Returns_Correct_Bool()
		{
			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.SetupGet(x => x.IsNullable).Returns(false);

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			edmStrcuturalProperty.SetupGet(x => x.Type).Returns(edmTypeReference.Object);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });
			entityType.Setup(x => x.FindProperty("Number")).Returns(edmStrcuturalProperty.Object);

			var edmTypeReference2 = new Mock<IEdmTypeReference>();
			edmTypeReference2.Setup(x => x.Definition).Returns(entityType.Object);
			var edmType = new Mock<IEdmCollectionType>();
			edmType.Setup(x => x.ElementType).Returns(edmTypeReference2.Object);

			var entitySet = CreateEntitySet("Materials");
			entitySet.SetupGet(x => x.Type).Returns(edmType.Object);
			var entityContainer = CreateEntityContainer(entitySet.Object);

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object, entityContainer.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.False(metadata.PropertyIsNullable("Materials", "Number"));
		}

		[Fact]
		public void PropertyIsNullable_With_EntityTypeName_And_Valid_Property_Returns_Correct_Bool()
		{
			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.SetupGet(x => x.IsNullable).Returns(true);

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			edmStrcuturalProperty.SetupGet(x => x.Type).Returns(edmTypeReference.Object);
			
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });
			entityType.Setup(x => x.FindProperty("Number")).Returns(edmStrcuturalProperty.Object);

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.True(metadata.PropertyIsNullable("Material", "Number"));
		}

		[Fact]
		public void PropertyIsNullable_With_EntityTypeName_And_InValid_Property_Throws_Exception()
		{
			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.SetupGet(x => x.IsNullable).Returns(false);

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			edmStrcuturalProperty.SetupGet(x => x.Type).Returns(edmTypeReference.Object);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.PropertyIsNullable("Material", "31"));
		}

		[Fact]
		public void PropertyIsNullable_With_EntitySetName_And_InValid_Property_Throws_Exception()
		{
			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.SetupGet(x => x.IsNullable).Returns(true);

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			edmStrcuturalProperty.SetupGet(x => x.Type).Returns(edmTypeReference.Object);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });
			
			var edmTypeReference2 = new Mock<IEdmTypeReference>();
			edmTypeReference2.Setup(x => x.Definition).Returns(entityType.Object);
			var edmType = new Mock<IEdmCollectionType>();
			edmType.Setup(x => x.ElementType).Returns(edmTypeReference2.Object);

			var entitySet = CreateEntitySet("Materials");
			entitySet.SetupGet(x => x.Type).Returns(edmType.Object);
			var entityContainer = CreateEntityContainer(entitySet.Object);

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object, entityContainer.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.PropertyIsNullable("Materials", "69"));
		}

		[Fact]
		public void PropertyIsNullable_With_Invalid_CollectionName_And_Valid_Property_Throws_Exception()
		{
			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.SetupGet(x => x.IsNullable).Returns(true);

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive);
			edmStrcuturalProperty.SetupGet(x => x.Type).Returns(edmTypeReference.Object);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.PropertyIsNullable("31", "Number"));
		}



		#region     Metadata string

		const string MetadataMaterialService = @"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
<edmx:Edmx xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx""
           Version=""4.0"">
  <edmx:DataServices>
    <Schema xmlns=""http://docs.oasis-open.org/odata/ns/edm""
            Namespace=""ErpServices.Services.MaterialService.Entities"">
      <EntityType Name=""Material"">
                <Key>
                    <PropertyRef Name=""Number"" />        
                </Key>
                <Property Name=""Number"" Type=""Edm.String"" Nullable=""false"" />
                <Property Name=""UnitOfMeasure"" Type=""Edm.String"" Nullable=""true"" />
                <Property Name=""Type"" Type=""Edm.String"" Nullable=""true"" />
                <NavigationProperty Name=""Descriptions"" Nullable=""true"" Type=""Collection(ErpServices.Services.MaterialService.Entities.MaterialDescription)"" Partner=""Material""/>
            </EntityType>
            <EntityType Name=""MaterialDescription"">
                <Key>
                    <PropertyRef Name=""Language"" />
                    <PropertyRef Name=""Number"" />        
                </Key>
                <Property Name=""Number"" Type=""Edm.String"" Nullable=""false"" />
                <Property Name=""Language"" Type=""Edm.String"" Nullable=""false"" />
                <Property Name=""Description"" Type=""Edm.String"" Nullable=""true"" />      
				<NavigationProperty Name=""Material"" Nullable=""true"" Type=""ErpServices.Services.MaterialService.Entities.Material"" Partner=""Descriptions""/>
            </EntityType>            
            <EntityContainer Name=""MaterialServiceAutoGenerated"" >
                <EntitySet Name=""Materials"" EntityType=""ErpServices.Services.MaterialService.Entities.Material"" >
					<NavigationPropertyBinding Path=""Descriptions"" Target=""MaterialDescriptions"" />
				</EntitySet>
                <EntitySet Name=""MaterialDescriptions"" EntityType=""ErpServices.Services.MaterialService.Entities.MaterialDescription"">
					<NavigationPropertyBinding Path=""Material"" Target=""Materials"" />
				</EntitySet>
            </EntityContainer>    
    </Schema>
  </edmx:DataServices>
</edmx:Edmx>";
		#endregion

		[Fact]
		public void GetNavigationPropertyPartnerMultiplicity_With_EntitySetName_And_Valid_Property_Returns_Correct_Multiplicity()
		{
			const EdmMultiplicity expectedMultiplicity = EdmMultiplicity.ZeroOrOne;
			var edmModel = ParseMetadata(MetadataMaterialService);

			var metadata = new Metadata(edmModel, new BestMatchResolver(), false, false);
			Assert.Same(expectedMultiplicity.ToString(), metadata.GetNavigationPropertyPartnerMultiplicity("Materials", "Descriptions"));
		}

		[Fact]
		public void GetNavigationPropertyPartnerMultiplicity_With_EntityTypeName_And_Valid_Property_Returns_Correct_Multiplicity()
		{
			const EdmMultiplicity expectedMultiplicity = EdmMultiplicity.Many;
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(edmModel, new BestMatchResolver(), false, false);
			Assert.Same(expectedMultiplicity.ToString(), metadata.GetNavigationPropertyPartnerMultiplicity("MaterialDescription", "Material"));
		}

		[Fact]
		public void GetNavigationPropertyPartnerMultiplicity_With_EntityTypeName_And_InValid_Property_Throws_Exception()
		{
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(edmModel, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetNavigationPropertyPartnerMultiplicity("Material", "Number"));
		}

		[Fact]
		public void GetNavigationPropertyPartnerMultiplicity_With_EntitySetName_And_InValid_Property_Throws_Exception()
		{
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(edmModel, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetNavigationPropertyPartnerMultiplicity("Materials", "Version"));
		}

		[Fact]
		public void GetNavigationPropertyPartnerMultiplicity_With_Invalid_EntityTypeName_And_Valid_Property_Throws_Exception()
		{
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(edmModel, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetNavigationPropertyPartnerMultiplicity("31", "Number"));
		}
		
		[Fact]
		public void GetNavigationPropertyMultiplicity_With_EntitySetName_And_Valid_Property_Returns_Correct_Multiplicity()
		{
			const EdmMultiplicity expectedMultiplicity = EdmMultiplicity.Many;
			var edmModel = ParseMetadata(MetadataMaterialService);

			var metadata = new Metadata(edmModel, new BestMatchResolver(), false, false);
			Assert.Same(expectedMultiplicity.ToString(), metadata.GetNavigationPropertyMultiplicity("Materials", "Descriptions"));
		}

		[Fact]
		public void GetNavigationPropertyMultiplicity_With_EntityTypeName_And_Valid_Property_Returns_Correct_Multiplicity()
		{
			const EdmMultiplicity expectedMultiplicity = EdmMultiplicity.ZeroOrOne;;
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(edmModel, new BestMatchResolver(), false, false);
			Assert.Same(expectedMultiplicity.ToString(), metadata.GetNavigationPropertyMultiplicity("MaterialDescription", "Material"));
		}

		[Fact]
		public void GetNavigationPropertyMultiplicity_With_EntityTypeName_And_InValid_Property_Throws_Exception()
		{
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(edmModel, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetNavigationPropertyMultiplicity("Material", "Number"));
		}

		[Fact]
		public void GetNavigationPropertyMultiplicity_With_EntitySetName_And_InValid_Property_Throws_Exception()
		{
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(edmModel, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetNavigationPropertyMultiplicity("Materials", "Version"));
		}

		[Fact]
		public void GetNavigationPropertyMultiplicity_With_Invalid_EntityTypeName_And_Valid_Property_Throws_Exception()
		{
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(edmModel, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetNavigationPropertyMultiplicity("31", "Number"));
		}

		[Fact]
		public void HasStream_For_Streamable_EntitySetName_Return_True()
		{
			var metadata = new Metadata(CreateStreamableModel(true), new BestMatchResolver(), false, false);
			Assert.True(metadata.HasStream("DocumentInfoRecordOriginalCollection"));
		}

		[Fact]
		public void HasStream_For_Streamable_EntityTypeName_Return_True()
		{
			var metadata = new Metadata(CreateStreamableModel(true), new BestMatchResolver(), false, false);
			Assert.True(metadata.HasStream("DocumentInfoRecordOriginal"));
		}

		[Fact]
		public void HasStream_For_NonStreamable_EntitySetName_Return_False()
		{
			var metadata = new Metadata(CreateStreamableModel(false), new BestMatchResolver(), false, false);
			Assert.False(metadata.HasStream("DocumentInfoRecordOriginalCollection"));
		}

		[Fact]
		public void HasStream_For_NonStreamable_EntityTypeName_Return_False()
		{
			var metadata = new Metadata(CreateStreamableModel(false), new BestMatchResolver(), false, false);
			Assert.False(metadata.HasStream("DocumentInfoRecordOriginal"));
		}

		[Fact]
		public void HasStream_With_Invalid_CollectioneName_Throws_Exception()
		{
			var metadata = new Metadata(CreateStreamableModel(false), new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.HasStream("31"));
		}

		[Fact]
		public void GetEntityTypeName_From_EntitySet_Will_Return_Right_one()
		{
			var edmStrcuturalProperty = CreateNavigationProperty("Number", EdmTypeKind.Collection);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });

			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.Setup(x => x.Definition).Returns(entityType.Object);
			var edmType = new Mock<IEdmCollectionType>();
			edmType.Setup(x => x.ElementType).Returns(edmTypeReference.Object);

			var entitySet = CreateEntitySet("Materials");
			entitySet.SetupGet(x => x.Type).Returns(edmType.Object);
			var entityContainer = CreateEntityContainer(entitySet.Object);

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object, entityContainer.Object });

			var metadata =new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Same("Material", metadata.GetEntityTypeName("Materials"));
		}

		[Fact]
		public void GetEntityTypeName_From_Invalid_EntitySet_Will_Throw_Exception()
		{
			var entityType = CreateEntityType("Material");
			var entitySet = CreateEntitySet("Materials");

			var entityContainer = CreateEntityContainer(entitySet.Object);

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object, entityContainer.Object });

			var metadata = new Metadata(edmModel.Object, new BestMatchResolver(), false, false);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetEntityTypeName("Blub"));
		}

		IEdmModel CreateStreamableModel(bool streamAble)
		{
			#region Metadata

			var metadataString = @"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
<edmx:Edmx xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx""
           Version=""4.0"">
  <edmx:DataServices>
    <Schema xmlns=""http://docs.oasis-open.org/odata/ns/edm""
            Namespace=""ErpServices.Services.DocumentInfoRecordService.Entities"">
            <EntityType Name=""DocumentInfoRecordOriginal"" HasStream=""" + streamAble.ToString().ToLower() + @""">
                <Key>
                    <PropertyRef Name=""Description"" />        
                </Key>
                <Property Name=""Description"" Type=""Edm.String"" Nullable=""false"" />
                <Property Name=""Value"" Type=""Edm.Binary"" Nullable=""true"" />      
            </EntityType>    
        
            <EntityContainer Name=""DocumentInfoRecordServiceAutoGenerated"" >
                <EntitySet Name=""DocumentInfoRecordOriginalCollection"" EntityType=""ErpServices.Services.DocumentInfoRecordService.Entities.DocumentInfoRecordOriginal"" />      
            </EntityContainer>  
		</Schema>
    </edmx:DataServices>
</edmx:Edmx>";

			#endregion

			var edmModel = ParseMetadata(metadataString);
			return edmModel;
		}

		IEdmModel ParseMetadata(string metadataString)
		{
			return CsdlReader.Parse(XmlReader.Create(new StringReader(metadataString)));
		}

		Mock<IEdmStructuralProperty> CreateProperty(string name, EdmTypeKind typeKind, EdmPrimitiveTypeKind primitiveTypeKind = EdmPrimitiveTypeKind.String)
		{
			var edmStrcuturalProperty = new Mock<IEdmStructuralProperty>();
			edmStrcuturalProperty.SetupGet(x => x.PropertyKind).Returns(EdmPropertyKind.Structural);
			edmStrcuturalProperty.SetupGet(x => x.Name).Returns(name);

			var edmDefinitionMock = new Mock<IEdmPrimitiveType>();
			edmDefinitionMock.SetupGet(x => x.TypeKind).Returns(typeKind);
			edmDefinitionMock.SetupGet(x => x.PrimitiveKind).Returns(primitiveTypeKind);

			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.SetupGet(x => x.Definition).Returns(edmDefinitionMock.Object);
			edmStrcuturalProperty.SetupGet(x => x.Type).Returns(edmTypeReference.Object);
			return edmStrcuturalProperty;
		}

		Mock<IEdmNavigationProperty> CreateNavigationProperty(string name, EdmTypeKind typeKind = EdmTypeKind.Entity)
		{
			var edmStrcuturalProperty = new Mock<IEdmNavigationProperty>();
			edmStrcuturalProperty.SetupGet(x => x.PropertyKind).Returns(EdmPropertyKind.Navigation);
			edmStrcuturalProperty.SetupGet(x => x.Name).Returns(name);

			var edmType = new Mock<IEdmTypeReference>();
			var edmDefinitionMock = new Mock<IEdmCollectionType>();
			edmDefinitionMock.SetupGet(x => x.TypeKind).Returns(typeKind);
			edmDefinitionMock.SetupGet(x => x.ElementType).Returns(edmType.Object);

			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.SetupGet(x => x.Definition).Returns(edmDefinitionMock.Object);
			edmStrcuturalProperty.SetupGet(x => x.Type).Returns(edmTypeReference.Object);
			return edmStrcuturalProperty;
		}

		Mock<IEdmEntityType> CreateEntityType(string name)
		{
			var entityType = new Mock<IEdmEntityType>();
			entityType.SetupGet(x => x.SchemaElementKind).Returns(EdmSchemaElementKind.TypeDefinition);
			entityType.SetupGet(x => x.TypeKind).Returns(EdmTypeKind.Entity);
			entityType.SetupGet(x => x.Name).Returns(name);
			return entityType;
		}

		Mock<IEdmEntitySet> CreateEntitySet(string name)
		{
			var entitySet = new Mock<IEdmEntitySet>();
			entitySet.SetupGet(x => x.ContainerElementKind).Returns(EdmContainerElementKind.EntitySet);
			entitySet.SetupGet(x => x.Name).Returns(name);
			return entitySet;
		}

		Mock<IEdmEntityContainer> CreateEntityContainer(params IEdmEntityContainerElement[] containerElements)
		{
			var entityContainer = new Mock<IEdmEntityContainer>();
			entityContainer.SetupGet(x => x.Elements).Returns(containerElements);
			entityContainer.SetupGet(x => x.SchemaElementKind).Returns(EdmSchemaElementKind.EntityContainer);
			return entityContainer;
		}
	}
}
