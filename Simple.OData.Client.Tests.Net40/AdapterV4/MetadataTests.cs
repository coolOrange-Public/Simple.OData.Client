using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Moq;
using Simple.OData.Client.V4.Adapter;
using Xunit;

namespace Simple.OData.Client.Tests.AdapterV4
{
	public class MetadataTestsv4
	{
		[Fact]
		public void GetPropertyNames_Model_With_One_EntitySet_ReturnsCorrect_Properties()
		{
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			var edmProperty = CreateProperty("Version", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
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

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
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

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			var edmProperty = CreateProperty("Version", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredKey).Returns(new[] { edmStrcuturalProperty.Object });
			entityType.Setup(x => x.DeclaredProperties).Returns(new List<IEdmProperty> { edmStrcuturalProperty.Object, edmProperty.Object, edmnavigationProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			var properties = metadata.GetPropertyNames("Material").ToList();
			Assert.Same("Version", properties.First());
			Assert.Equal(1, properties.Count);
		}
		[Fact]
		public void GetEntitySetNames_Model_With_One_EntitySet_Returns_One_EntitySetName()
		{
			var session = new Mock<ISession>();
			const string expectedEntitySetName = "Materials";
			var entitySet = CreateEntitySet(expectedEntitySetName);
			var entityContainer = CreateEntityContainer(entitySet.Object);
			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityContainer.Object });

			var metadata = new Metadata(session.Object, edmModel.Object);
			var entitySetNames = metadata.GetEntitySetNames().ToList();
			Assert.Same(expectedEntitySetName, entitySetNames.First());
			Assert.Equal(1, entitySetNames.Count);
		}

		[Fact]
		public void GetEntitySetNames_Model_With_No_EntitySet_Returns_An_Empty_List()
		{
			var session = new Mock<ISession>();
			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement>());

			Assert.Empty(new Metadata(session.Object, edmModel.Object).GetEntitySetNames());
		}

		[Fact]
		public void GetEntityTypeNames_Model_With_Two_EntityTypes_Returns_Two_EntityTypeNames()
		{
			var session = new Mock<ISession>();
			const string expectedEntityTypeName = "Material";
			var entityType = CreateEntityType(expectedEntityTypeName);
			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });
			
			var metadata = new Metadata(session.Object, edmModel.Object);
			var entityTypeNames = metadata.GetEntityTypeNames().ToList();
			Assert.Same(expectedEntityTypeName, entityTypeNames.First());
			Assert.Equal(1, entityTypeNames.Count);
		}

		[Fact]
		public void GetEntityTypeNames_Model_With_No_EntityType_Returns_An_Empty_List()
		{
			var session = new Mock<ISession>();
			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement>());
			Assert.Equal(0, new Metadata(session.Object, edmModel.Object).GetEntityTypeNames().Count());
		}

		[Fact]
		public void GetNavigationPropertyNames_Returns_On_A_Model_With_Two_EntityTypes_For_EntitySetName_One_NavigationProperty()
		{
			var navigationEntityType = CreateEntityType("Description");

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
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

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			var navigationPropertyNames = metadata.GetNavigationPropertyNames("Materials").ToList();
			Assert.Equal(1, navigationPropertyNames.Count);
			Assert.Equal("Description", navigationPropertyNames.First());
		}

		[Fact]
		public void GetNavigationPropertyNames_Returns_On_A_Model_With_Two_EntityTypes_For_EntityTypeName_One_NavigationProperty()
		{
			var navigationEntityType = CreateEntityType("Description");

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
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

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			var navigationPropertyNames = metadata.GetNavigationPropertyNames("Material").ToList();
			Assert.Equal(1, navigationPropertyNames.Count);
			Assert.Equal("Description", navigationPropertyNames.First());
		}

		[Fact]
		public void GetNavigationPropertyNames_Model_With_No_NavigationProperties_Returns_An_Empty_List()
		{
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
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

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			Assert.Empty(metadata.GetNavigationPropertyNames("Materials"));
		}

		[Fact]
		public void GetNavigationPropertyNames_Model_With_Wrong_CollectionName_Throws_Exception()
		{
			var entitySet = CreateEntitySet("Materials");
			var entityContainer = CreateEntityContainer(entitySet.Object);
			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityContainer.Object });

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			Assert.Throws<UnresolvableObjectException>(()=>metadata.GetNavigationPropertyNames("31"));
		}

		[Fact]
		public void GetNavigationPropertyNames_Model_With_One_EntitySet_One_NormalProperty_One_NavigationProperty_Returns_One_NavigationProperty()
		{
			var navigationEntityType = CreateEntityType("Description");

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			var edmNormalProperty = CreateProperty("Version", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
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

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			var navigationPropertyNames = metadata.GetNavigationPropertyNames("Materials").ToList();
			Assert.Equal(1, navigationPropertyNames.Count);
			Assert.Equal("Description", navigationPropertyNames.First());
		}


		[Fact]
		public void GetPropertyType_Model_With_Two_EntityTypes_Returns_Correct_Type_For_KeyProperty_Of_Passed_EntityTypeName()
		{
			var expectedType = typeof(int);
			var edmStrcuturalProperty = CreateProperty("Id", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });

			var entityType2 = CreateEntityType("Description");
			var edmStrcuturalProperty2 = CreateProperty("Id", EdmTypeKind.Primitive, EdmPropertyKind.None);
			entityType2.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty2.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object, entityType2.Object });

			var edmTypeMap = new Mock<IEdmTypeMap>();
			edmTypeMap.Setup(x => x.GetTypes(edmStrcuturalProperty.Object.Type)).Returns(new[] { expectedType });
			var session = new Mock<ISession>();

			var metadata = new Metadata(session.Object, edmModel.Object) { EdmTypeMap = edmTypeMap.Object };
			Assert.Same(expectedType, metadata.GetPropertyType("Material", "Id"));
		}

		[Fact]
		public void GetPropertyType_Model_With_One_EntityType_Returns_Correct_Type_For_NormalProperty_Of_Passed_EntityTypeName()
		{
			var expectedType = typeof(bool);
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });

			var entityType2 = CreateEntityType("Description");
			var edmStrcuturalProperty2 = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.None);
			entityType2.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty2.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object, entityType2.Object });

			var edmTypeMap = new Mock<IEdmTypeMap>();
			edmTypeMap.Setup(x => x.GetTypes(edmStrcuturalProperty.Object.Type)).Returns(new[] { expectedType });
			var session = new Mock<ISession>();

			var metadata = new Metadata(session.Object, edmModel.Object) { EdmTypeMap = edmTypeMap.Object };
			Assert.Same(expectedType, metadata.GetPropertyType("Material", "Number"));
		}

		[Fact]
		public void GetPropertyType_Model_With_Invalid_CollectionName_Throws_Exception()
		{
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredKey).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });
			var session = new Mock<ISession>();

			var metadata = new Metadata(session.Object, edmModel.Object);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetPropertyType("Invalid", "Number"));
		}

		[Fact]
		public void GetPropertyType_Model_With_Invalid_PropertyName_Throws_Exception()
		{
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredKey).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });
			var session = new Mock<ISession>();

			var metadata = new Metadata(session.Object, edmModel.Object);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetPropertyType("Material", "Invalid"));
		}

		[Fact]
		public void GetPropertyType_Model_With_One_EntitySet_Returns_List_Of_objects_Type_For_NavigationProperty_Which_IsCollection_Of_Passed_EntitySetName()
		{
			var expectedType = typeof(IEnumerable<object>);
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

			var edmTypeMap = new Mock<IEdmTypeMap>();
			edmTypeMap.Setup(x => x.GetTypes(edmStrcuturalProperty.Object.Type)).Returns(new[] { expectedType });
			var session = new Mock<ISession>();

			var metadata = new Metadata(session.Object, edmModel.Object) { EdmTypeMap = edmTypeMap.Object };
			Assert.Same(expectedType, metadata.GetPropertyType("Materials", "Number"));
		}

		[Fact]
		public void GetPropertyDefaultValue_With_EntitySetName_And_Valid_Property_Returns_Correct_String()
		{
			const string expectedDefaultValue = "31";
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			edmStrcuturalProperty.SetupGet(x => x.DefaultValueString).Returns(expectedDefaultValue);
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
			
			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			Assert.Same(expectedDefaultValue, metadata.GetPropertyDefaultValue("Materials", "Number"));
		}

		[Fact]
		public void GetPropertyDefaultValue_With_EntityTypeName_And_Valid_Property_Returns_Correct_String()
		{
			const string expectedDefaultValue = "69";
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			edmStrcuturalProperty.SetupGet(x => x.DefaultValueString).Returns(expectedDefaultValue);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			Assert.Same(expectedDefaultValue, metadata.GetPropertyDefaultValue("Material", "Number"));
		}

		[Fact]
		public void GetPropertyDefaultValue_With_EntityTypeName_And_InValid_Property_Throws_Exception()
		{
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			edmStrcuturalProperty.SetupGet(x => x.DefaultValueString).Returns("31");
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetPropertyDefaultValue("Material", "Exception"));
		}

		[Fact]
		public void GetPropertyDefaultValue_With_EntitySetName_And_InValid_Property_Throws_Exception()
		{
			var entitySet = CreateEntitySet("Materials");
			var entityContainer = CreateEntityContainer(entitySet.Object);

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityContainer.Object });

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetPropertyDefaultValue("Material", "Ehy"));
		}

		[Fact]
		public void GetPropertyDefaultValue_With_Invalid_CollectionName_And_Valid_Property_Throws_Exception()
		{
			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			edmStrcuturalProperty.SetupGet(x => x.DefaultValueString).Returns("31");
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredKey).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetPropertyDefaultValue("31", "Number"));
		}

		[Fact]
		public void PropertyIsNullable_With_EntitySetName_And_Valid_Property_Returns_Correct_Bool()
		{
			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.SetupGet(x => x.IsNullable).Returns(false);

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
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

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			Assert.False(metadata.PropertyIsNullable("Materials", "Number"));
		}

		[Fact]
		public void PropertyIsNullable_With_EntityTypeName_And_Valid_Property_Returns_Correct_Bool()
		{
			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.SetupGet(x => x.IsNullable).Returns(true);

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			edmStrcuturalProperty.SetupGet(x => x.Type).Returns(edmTypeReference.Object);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			Assert.True(metadata.PropertyIsNullable("Material", "Number"));
		}

		[Fact]
		public void PropertyIsNullable_With_EntityTypeName_And_InValid_Property_Throws_Exception()
		{
			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.SetupGet(x => x.IsNullable).Returns(false);

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			edmStrcuturalProperty.SetupGet(x => x.Type).Returns(edmTypeReference.Object);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			Assert.Throws<UnresolvableObjectException>(() => metadata.PropertyIsNullable("Material", "31"));
		}

		[Fact]
		public void PropertyIsNullable_With_EntitySetName_And_InValid_Property_Throws_Exception()
		{
			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.SetupGet(x => x.IsNullable).Returns(true);

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
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

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			Assert.Throws<UnresolvableObjectException>(() => metadata.PropertyIsNullable("Materials", "69"));
		}

		[Fact]
		public void PropertyIsNullable_With_Invalid_CollectionName_And_Valid_Property_Throws_Exception()
		{
			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.SetupGet(x => x.IsNullable).Returns(true);

			var edmStrcuturalProperty = CreateProperty("Number", EdmTypeKind.Primitive, EdmPropertyKind.Structural);
			edmStrcuturalProperty.SetupGet(x => x.Type).Returns(edmTypeReference.Object);
			var entityType = CreateEntityType("Material");
			entityType.SetupGet(x => x.DeclaredProperties).Returns(new[] { edmStrcuturalProperty.Object });

			var edmModel = new Mock<IEdmModel>();
			edmModel.SetupGet(x => x.SchemaElements).Returns(new List<IEdmSchemaElement> { entityType.Object });

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
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

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel);
			Assert.Same(expectedMultiplicity.ToString(), metadata.GetNavigationPropertyPartnerMultiplicity("Materials", "Descriptions"));
		}

		[Fact]
		public void GetNavigationPropertyPartnerMultiplicity_With_EntityTypeName_And_Valid_Property_Returns_Correct_Multiplicity()
		{
			const EdmMultiplicity expectedMultiplicity = EdmMultiplicity.Many;
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(new Mock<ISession>().Object, edmModel);
			Assert.Same(expectedMultiplicity.ToString(), metadata.GetNavigationPropertyPartnerMultiplicity("MaterialDescription", "Material"));
		}

		[Fact]
		public void GetNavigationPropertyPartnerMultiplicity_With_EntityTypeName_And_InValid_Property_Throws_Exception()
		{
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(new Mock<ISession>().Object, edmModel);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetNavigationPropertyPartnerMultiplicity("Material", "Number"));
		}

		[Fact]
		public void GetNavigationPropertyPartnerMultiplicity_With_EntitySetName_And_InValid_Property_Throws_Exception()
		{
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(new Mock<ISession>().Object, edmModel);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetNavigationPropertyPartnerMultiplicity("Materials", "Version"));
		}

		[Fact]
		public void GetNavigationPropertyPartnerMultiplicity_With_Invalid_EntityTypeName_And_Valid_Property_Throws_Exception()
		{
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(new Mock<ISession>().Object, edmModel);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetNavigationPropertyPartnerMultiplicity("31", "Number"));
		}
		
		[Fact]
		public void GetNavigationPropertyMultiplicity_With_EntitySetName_And_Valid_Property_Returns_Correct_Multiplicity()
		{
			const EdmMultiplicity expectedMultiplicity = EdmMultiplicity.Many;
			var edmModel = ParseMetadata(MetadataMaterialService);

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel);
			Assert.Same(expectedMultiplicity.ToString(), metadata.GetNavigationPropertyMultiplicity("Materials", "Descriptions"));
		}

		[Fact]
		public void GetNavigationPropertyMultiplicity_With_EntityTypeName_And_Valid_Property_Returns_Correct_Multiplicity()
		{
			const EdmMultiplicity expectedMultiplicity = EdmMultiplicity.ZeroOrOne;;
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(new Mock<ISession>().Object, edmModel);
			Assert.Same(expectedMultiplicity.ToString(), metadata.GetNavigationPropertyMultiplicity("MaterialDescription", "Material"));
		}

		[Fact]
		public void GetNavigationPropertyMultiplicity_With_EntityTypeName_And_InValid_Property_Throws_Exception()
		{
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(new Mock<ISession>().Object, edmModel);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetNavigationPropertyMultiplicity("Material", "Number"));
		}

		[Fact]
		public void GetNavigationPropertyMultiplicity_With_EntitySetName_And_InValid_Property_Throws_Exception()
		{
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(new Mock<ISession>().Object, edmModel);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetNavigationPropertyMultiplicity("Materials", "Version"));
		}

		[Fact]
		public void GetNavigationPropertyMultiplicity_With_Invalid_EntityTypeName_And_Valid_Property_Throws_Exception()
		{
			var edmModel = ParseMetadata(MetadataMaterialService);
			var metadata = new Metadata(new Mock<ISession>().Object, edmModel);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetNavigationPropertyMultiplicity("31", "Number"));
		}

		[Fact]
		public void HasStream_For_Streamable_EntitySetName_Return_True()
		{
			var metadata = new Metadata(new Mock<ISession>().Object, CreateStreamableModel(true));
			Assert.True(metadata.HasStream("DocumentInfoRecordOriginalCollection"));
		}

		[Fact]
		public void HasStream_For_Streamable_EntityTypeName_Return_True()
		{
			var metadata = new Metadata(new Mock<ISession>().Object, CreateStreamableModel(true));
			Assert.True(metadata.HasStream("DocumentInfoRecordOriginal"));
		}

		[Fact]
		public void HasStream_For_NonStreamable_EntitySetName_Return_False()
		{
			var metadata = new Metadata(new Mock<ISession>().Object, CreateStreamableModel(false));
			Assert.False(metadata.HasStream("DocumentInfoRecordOriginalCollection"));
		}

		[Fact]
		public void HasStream_For_NonStreamable_EntityTypeName_Return_False()
		{
			var metadata = new Metadata(new Mock<ISession>().Object, CreateStreamableModel(false));
			Assert.False(metadata.HasStream("DocumentInfoRecordOriginal"));
		}

		[Fact]
		public void HasStream_With_Invalid_CollectioneName_Throws_Exception()
		{
			var metadata = new Metadata(new Mock<ISession>().Object, CreateStreamableModel(false));
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

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			Assert.Same("Material", metadata.GetEntityTypeName("Materials"));
		}

		[Fact]
		public void GetEntityTypeName_From__Invalid_EntitySet_Will_Throw_Exception()
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

			var metadata = new Metadata(new Mock<ISession>().Object, edmModel.Object);
			Assert.Throws<UnresolvableObjectException>(() => metadata.GetEntityTypeName("Material"));
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
			return EdmxReader.Parse(XmlReader.Create(new StringReader(metadataString)));
		}

		Mock<IEdmStructuralProperty> CreateProperty(string name, EdmTypeKind typeKind, EdmPropertyKind propertyKind)
		{
			return CreateProperty<IEdmStructuralProperty>(name, typeKind, propertyKind);
		}

		Mock<IEdmNavigationProperty> CreateNavigationProperty(string name, EdmTypeKind typeKind = EdmTypeKind.Entity)
		{
			return CreateProperty<IEdmNavigationProperty>(name, typeKind, EdmPropertyKind.Navigation);
		}

		Mock<T> CreateProperty<T>(string name, EdmTypeKind typeKind, EdmPropertyKind propertyKind) where T : class, IEdmProperty
		{
			var edmStrcuturalProperty = new Mock<T>();
			edmStrcuturalProperty.SetupGet(x => x.PropertyKind).Returns(propertyKind);
			edmStrcuturalProperty.SetupGet(x => x.Name).Returns(name);

			var edmDefinition = new Mock<IEdmType>();
			edmDefinition.SetupGet(x => x.TypeKind).Returns(typeKind);

			var edmTypeReference = new Mock<IEdmTypeReference>();
			edmTypeReference.SetupGet(x => x.Definition).Returns(edmDefinition.Object);
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
