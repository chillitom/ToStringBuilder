ToStringBuilder takes the tedium out of creating and maintaining ToString() methods yet the created code is super fast and refactor proof.  

BSD licensed, just copy paste the code into your own projects and away you go.

        public class Example
        {
            public string PublicField = "Hello";
            private int _privateField = 99;

            // builder can be defined and compiled once when the class is loaded
            private static readonly ToStringBuilder<Example> ExampleToStringBuilder = new ToStringBuilder<Example>()
                .Include(e => e.PublicField)    // select fields to include
                .Include(e => e._privateField)  // n.b. refactor safe
                .MultiLine(false)               // optionally select formatting
                .Compile();                     // compile to a fast ToString method

            public override string ToString()
            {
                return ExampleToStringBuilder.Stringify(this);
            }
        }
        
        new Example().ToString(); // returns.. "Example{PublicField:\"Hello\",_privateField:99}"

Compare this to the hand coded alternative:

       public class Example
       {
            public string PublicField = "Hello";
            private int _privateField = 99;

            public override string ToString()
            {
                return new StringBuilder()
                   .Append(typeof(Example).Name) // looked up each time
                   .Append("{"PublicField:\"")   // formatting and field names get mushed up
                   .Append(PublicField)
                   .Append("\",")                // strings not quoted automatically
                   .Append("_privateField:")     // not refactor safe
                   .Append(_privateField)
                   .Append('}').ToString();
            }
       }

