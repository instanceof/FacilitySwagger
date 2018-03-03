using System;
using System.Collections.Generic;
using System.Linq;

namespace Facility.Definition
{
	/// <summary>
	/// Information about a service from a definition.
	/// </summary>
	public sealed class ServiceInfo : IServiceMemberInfo
	{
		/// <summary>
		/// Creates a service.
		/// </summary>
		public ServiceInfo(string name, IEnumerable<IServiceMemberInfo> members = null, IEnumerable<ServiceAttributeInfo> attributes = null, string summary = null, IEnumerable<string> remarks = null, NamedTextPosition position = null)
			: this(ValidationMode.Throw, name, members, attributes, summary, remarks, position)
		{
		}

		/// <summary>
		/// Creates a service.
		/// </summary>
		internal ServiceInfo(ValidationMode validationMode, string name, IEnumerable<IServiceMemberInfo> members = null, IEnumerable<ServiceAttributeInfo> attributes = null, string summary = null, IEnumerable<string> remarks = null, NamedTextPosition position = null)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Members = members.ToReadOnlyList();
			Attributes = attributes.ToReadOnlyList();
			Summary = summary ?? "";
			Remarks = remarks.ToReadOnlyList();
			Position = position;

			m_membersByName = Members.ToLookup(x => x.Name);

			if (validationMode == ValidationMode.Throw)
				GetValidationErrors().ThrowIfAny();
		}

		/// <summary>
		/// The service name.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// All of the service members..
		/// </summary>
		public IReadOnlyList<IServiceMemberInfo> Members { get; }

		/// <summary>
		/// The methods.
		/// </summary>
		public IReadOnlyList<ServiceMethodInfo> Methods => Members.OfType<ServiceMethodInfo>().ToReadOnlyList();

		/// <summary>
		/// The DTOs.
		/// </summary>
		public IReadOnlyList<ServiceDtoInfo> Dtos => Members.OfType<ServiceDtoInfo>().ToReadOnlyList();

		/// <summary>
		/// The enumerated types.
		/// </summary>
		public IReadOnlyList<ServiceEnumInfo> Enums => Members.OfType<ServiceEnumInfo>().ToReadOnlyList();

		/// <summary>
		/// The error sets.
		/// </summary>
		public IReadOnlyList<ServiceErrorSetInfo> ErrorSets => Members.OfType<ServiceErrorSetInfo>().ToReadOnlyList();

		/// <summary>
		/// The service attributes.
		/// </summary>
		public IReadOnlyList<ServiceAttributeInfo> Attributes { get; }

		/// <summary>
		/// The service summary.
		/// </summary>
		public string Summary { get; }

		/// <summary>
		/// The service remarks.
		/// </summary>
		public IReadOnlyList<string> Remarks { get; }

		/// <summary>
		/// The position of the service.
		/// </summary>
		public NamedTextPosition Position { get; }

		/// <summary>
		/// Returns any definition errors.
		/// </summary>
		public IEnumerable<ServiceDefinitionError> GetValidationErrors()
		{
			foreach (var error in ServiceDefinitionUtility.ValidateName(Name, Position))
				yield return error;

			foreach (var member in Members)
			{
				if (!(member is ServiceMethodInfo) && !(member is ServiceDtoInfo) && !(member is ServiceEnumInfo) && !(member is ServiceErrorSetInfo))
					yield return new ServiceDefinitionError($"Unsupported member type '{member.GetType()}'.", member.Position);
			}

			foreach (var error in ServiceDefinitionUtility.ValidateNoDuplicateNames(Members, "service member"))
				yield return error;

			foreach (var field in Methods.SelectMany(x => x.RequestFields.Concat(x.ResponseFields)).Concat(Dtos.SelectMany(x => x.Fields)))
			{
				ServiceTypeInfo.TryParse(field.TypeName, FindMember, field.TypeNamePosition, out var error);
				if (error != null)
					yield return error;
			}

			foreach (var error in Methods.SelectMany(x => x.GetValidationErrors()))
				yield return error;
			foreach (var error in Dtos.SelectMany(x => x.GetValidationErrors()))
				yield return error;
			foreach (var error in Enums.SelectMany(x => x.GetValidationErrors()))
				yield return error;
			foreach (var error in ErrorSets.SelectMany(x => x.GetValidationErrors()))
				yield return error;
		}

		/// <summary>
		/// Finds the member of the specified name.
		/// </summary>
		public IServiceMemberInfo FindMember(string name)
		{
			return m_membersByName[name].SingleOrDefault();
		}

		/// <summary>
		/// Gets the type of the specified name.
		/// </summary>
		public ServiceTypeInfo GetType(string typeName)
		{
			return ServiceTypeInfo.Parse(typeName, FindMember);
		}

		/// <summary>
		/// Attempts to get the type of the specified name.
		/// </summary>
		public ServiceTypeInfo TryGetType(string typeName, out ServiceDefinitionError error)
		{
			return ServiceTypeInfo.TryParse(typeName, FindMember, null, out error);
		}

		/// <summary>
		/// Gets the field type for a field.
		/// </summary>
		public ServiceTypeInfo GetFieldType(ServiceFieldInfo field)
		{
			return ServiceTypeInfo.Parse(field.TypeName, FindMember, field.TypeNamePosition);
		}

		/// <summary>
		/// Attempts to get the field type for a field.
		/// </summary>
		public ServiceTypeInfo TryGetFieldType(ServiceFieldInfo field, out ServiceDefinitionError error)
		{
			return ServiceTypeInfo.TryParse(field.TypeName, FindMember, field.TypeNamePosition, out error);
		}

		readonly ILookup<string, IServiceMemberInfo> m_membersByName;
	}
}
